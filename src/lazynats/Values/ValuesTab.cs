using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Text;
using lazynats.Components;
using lazynats.Core;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Values;

// Two levels (bucket list / key list) sharing one screen region: rather than tearing down and
// rebuilding views on every descend/ascend, both levels' EditFrame+list+details are built once
// and kept alive, toggled via Visible (EditFrame wraps a fixed child, so it can't itself swap
// between BucketListView/KeyListView - see StreamsTab, which this deliberately mirrors).
// _currentBucket is null at the bucket level, and holds the drilled-into bucket's name at the key
// level.
internal sealed class ValuesTab: View, IShortcutSource
{
    // Every key-list fetch stops once this many matches are collected - including an unfiltered
    // one, which compiles down to native filter `>` (matches everything) - regardless of how many
    // more an over-approximated native filter could still return from a huge bucket.
    private const int KeyFilterCap = 10_000;

    private readonly INatsKVContext _kv;

    private readonly ObservableCollection<KvBucketItem> _items = [];
    private readonly BucketListView _listView;
    private readonly FilterBox _bucketFilterBox;
    private readonly EditFrame _bucketListFrame;
    private readonly BucketDetails _details;

    private readonly ObservableCollection<string> _keyItems = [];
    private readonly KeyListView _keyListView;
    private readonly FilterBox _keyFilterBox;
    private readonly EditFrame _keyListFrame;
    private readonly KeyDetails _keyDetails;

    private readonly Label _listLabel;
    private readonly Label _detailsLabel;

    // Guards the one-time initial GetStatusesAsync call - the list is otherwise load-once +
    // Ctrl+R only, per nats-kv's "Manual Bucket List Refresh" requirement.
    private bool _loaded;

    // Null at the bucket level; the drilled-into bucket's name at the key level.
    private string? _currentBucket;

    // Whether the most recently completed key-list fetch hit KeyFilterCap - drives
    // UpdateKeyListTitle's truncation indicator. Only ever true while a filter is active; reset
    // alongside _keyListView's own active filter on Descend/Ascend for the same reason.
    private bool _keyListTruncated;

    public event Action<string>? StatusChanged;

    public ValuesTab(INatsKVContext kv)
    {
        CanFocus = true;
        _kv = kv;

        _listLabel = new Label { Text = "Buckets", X = 0, Y = 0 };
        _bucketFilterBox = new FilterBox { X = 0, Y = 1, Width = Dim.Percent(40) };
        _listView = new BucketListView(_items) { Background = Theme.EditableBackground };
        _listView.AttachFilterBox(_bucketFilterBox);
        _bucketListFrame = new EditFrame(_listView) {
            X = 0, Y = Pos.Bottom(_bucketFilterBox), Width = Dim.Percent(40), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _listView.RefreshRequested += () => _ = RefreshListAsync();
        _listView.HighlightChanged += OnBucketHighlightChanged;
        _listView.DescendRequested += Descend;
        _listView.CreateRequested += () => OpenCreateBucketDialog(null);
        _listView.DeleteRequested += () => _ = TryDeleteBucketAsync();
        _listView.EditRequested += OpenEditBucketDialog;

        _keyFilterBox = new FilterBox { X = 0, Y = 1, Width = Dim.Percent(40), Visible = false };
        _keyListView = new KeyListView(_keyItems) { Background = Theme.EditableBackground };
        _keyListView.AttachFilterBox(_keyFilterBox);
        _keyListFrame = new EditFrame(_keyListView) {
            X = 0, Y = Pos.Bottom(_keyFilterBox), Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _keyListView.RefreshRequested += () => _ = RefreshKeyListAsync();
        _keyListView.HighlightChanged += OnKeyHighlightChanged;
        _keyListView.AscendRequested += Ascend;
        _keyListView.CreateRequested += () => OpenCreateKeyDialog(null);
        _keyListView.DeleteRequested += () => _ = TryDeleteKeyAsync();
        _keyListView.EditRequested += OpenEditKeyDialog;
        // KeyListView (via the shared DrillableListView<T> Filter wiring) owns the Ctrl+F
        // dialog/compile/in-memory-narrow mechanics itself; this tab only needs to additionally
        // scope its server-side fetch and reflect the active pattern in the list's title - the one
        // list in the app whose backing store supports that. See
        // openspec/changes/unify-list-filtering/design.md Decision 2.
        _keyListView.FilterChanged += pattern => { UpdateKeyListTitle(); _ = RefreshKeyListAsync(); };

        _detailsLabel = new Label { Text = "Details", X = Pos.Right(_bucketListFrame) + 1, Y = 0 };
        _details = new BucketDetails(_kv) { X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _details.Error += message => StatusChanged?.Invoke($"Values: {message}");

        _keyDetails = new KeyDetails(_kv) {
            X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false,
        };
        _keyDetails.Error += message => StatusChanged?.Invoke($"Values: {message}");

        // Add()-order matches spatial top-down layout (label, then FilterBox, then its list) so
        // Tab/Shift+Tab cycles in reading order - ManagementTabs.FindFirstFocusableDescendant
        // separately skips FilterBox for the tab's *default* focus target, so entering/returning
        // to this tab still lands on the list, not the search field, despite that order.
        Add(_listLabel, _bucketFilterBox, _bucketListFrame, _keyFilterBox, _keyListFrame, _detailsLabel, _details, _keyDetails);

        SetShortcutSource(_listView);
    }

    // Whichever list is currently the visible/active one - the bucket list at the top level, the
    // key list once drilled in. Updated alongside every other piece of level-toggling state in
    // Descend/Ascend, not computed from _currentBucket - see openspec/specs/tab-scoped-list-shortcuts/
    // spec.md. Backs both OnKeyDownNotHandled and Shortcuts below, so dispatch and advertisement read
    // the same list and can't drift apart.
    private ITabOperationsSource _shortcutSource = null!;

    private void SetShortcutSource(ITabOperationsSource source) => _shortcutSource = source;

    // No KeyBindings/AddCommand for Ctrl+R/N/D/E/F here - that would hardcode which keys this tab
    // forwards. Instead this fires once Terminal.Gui has already tried the focused view (and its own
    // ancestors, including whichever list is focused) and found no handler, at which point it's
    // this tab's turn; whatever key the currently active list's own TabOperations happens to expose
    // is what gets dispatched, so a list is free to add a new operation without this tab needing to
    // know about it in advance.
    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (_shortcutSource.TabOperations.FirstOrDefault(h => h.Key == key) is { Action: { } action }) {
            action();
            return true;
        }

        return base.OnKeyDownNotHandled(key);
    }

    public IEnumerable<ShortcutHint> Shortcuts => _shortcutSource.TabOperations;

    // "Selected tab" in this app is focus-driven (doc/terminal-gui-howto.md: "The focused SubView
    // is the selected (front-most) tab") - so this fires exactly on tab entry/exit, which is what
    // gates the one-time initial load and the detail poll, per the "Refresh does not run while the
    // tab is not selected" requirement. Polling itself is owned by each *Details pane (see their
    // own comments) - this just tells whichever level is current whether it's allowed to fire.
    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);

        if (newHasFocus && !_loaded) {
            _loaded = true;
            _ = RefreshListAsync();
        }

        if (_currentBucket is null) _details.SetActive(newHasFocus);
        else _keyDetails.SetActive(newHasFocus);
    }

    private void OnBucketHighlightChanged(KvBucketItem? item)
    {
        _details.SetTarget(item?.Name);
        _details.Show(item?.Status);
    }

    private void OnKeyHighlightChanged(string? key)
    {
        _keyDetails.SetTarget(_currentBucket, key);
        // Unlike Stream/Consumer/Bucket, the key list only carries bare names - there's no cached
        // entry to Show() instantly, so clear first (no stale flash of the previous key's value).
        // SetTarget above already schedules a debounced fetch of the new key - see
        // PollingDetailsView's "Immediate Fetch On Demand".
        _keyDetails.Show(null);
    }

    // Enter on a highlighted bucket. Always fetches the key list fresh (nats-kv's "Key List"
    // requirement - no per-bucket caching, even re-descending into the same bucket just left) and
    // never re-fetches the bucket list on the way in.
    private void Descend()
    {
        if (_listView.SelectedBucket is not { } item) return;

        var name = item.Name;
        _currentBucket = name;
        // A filter never carries over into a (possibly different) bucket entered by a fresh
        // descent - see "Server-Side Key Filter"'s reset-on-descend requirement. Silent: this tab
        // is about to fetch unconditionally below regardless, so a FilterChanged-driven re-fetch
        // here would just be redundant.
        _keyListView.ClearFilterSilently();
        _keyListTruncated = false;
        // Clear before showing - otherwise whatever a *previous* descent left behind (a different
        // bucket's keys, or a stale highlight) would flash for a frame until the fetch below
        // resolves. Clearing the list cascades into clearing the details pane too, via the same
        // HighlightChanged wiring an empty Ctrl+R result already goes through.
        _keyListView.ReplaceItems([]);
        UpdateKeyListTitle();
        _detailsLabel.Text = "Key Details";
        _bucketFilterBox.Visible = false;
        _bucketListFrame.Visible = false;
        _keyFilterBox.Visible = true;
        _keyListFrame.Visible = true;
        _details.Visible = false;
        _keyDetails.Visible = true;

        _details.SetActive(false);
        _keyDetails.SetActive(HasFocus);
        SetShortcutSource(_keyListView);

        _keyListView.SetFocus();
        _ = RefreshKeyListAsync();
    }

    // Esc/Backspace from the key level. Never re-fetches the bucket list - it just re-shows
    // BucketListView's already-loaded state (nats-kv's "Manual Bucket List Refresh" requirement:
    // the bucket list only ever changes via explicit Ctrl+R).
    private void Ascend()
    {
        _currentBucket = null;
        _keyListView.ClearFilterSilently();
        _keyListTruncated = false;
        _listLabel.Text = "Buckets";
        _detailsLabel.Text = "Details";
        _keyFilterBox.Visible = false;
        _keyListFrame.Visible = false;
        _bucketFilterBox.Visible = true;
        _bucketListFrame.Visible = true;
        _keyDetails.Visible = false;
        _details.Visible = true;

        _keyDetails.SetActive(false);
        _details.SetActive(HasFocus);
        SetShortcutSource(_listView);

        _listView.SetFocus();
    }

    // Always runs on the UI thread - either directly from the Ctrl+N key command (already on the
    // UI thread) or via the App.Invoke below, reached from TryCreateBucketAsync's continuation
    // after an await. Mirrors StreamsTab.OpenCreateStreamDialog exactly - see its comment for the
    // full rationale.
    private void OpenCreateBucketDialog(NewBucketOptions? seed)
    {
        var dialog = new CreateBucketDialog(seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryCreateBucketAsync(options);
    }

    private async Task TryCreateBucketAsync(NewBucketOptions options)
    {
        try {
            await _kv.CreateStoreAsync(options.ToNatsKVConfig());
            _ = RefreshListAsync(options.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Create Bucket Failed ", ex.Message.Pad(), "_Ok");
                OpenCreateBucketDialog(options);
            });
        }
    }

    // Scoped by _listView.SelectedBucket alone, same as TryDeleteBucketAsync - Ctrl+E is only
    // bound at the bucket level (see BucketListView), unreachable from the key level.
    private void OpenEditBucketDialog()
    {
        if (_listView.SelectedBucket is { } item) OpenEditBucketDialog(item, ToNatsKVConfig(item), null);
    }

    // `original` is the freshly-fetched NatsKVConfig to merge edits onto later (built once here,
    // not re-derived from `seed` on a reopen-after-failure, so a rejected attempt still merges
    // against the real server-side config rather than the user's typed values). `seed` reopens
    // with the previously-entered values after a failed edit (design.md Decision 5); omitted on
    // the initial Ctrl+E, where the dialog is seeded straight from `original` instead.
    private void OpenEditBucketDialog(KvBucketItem item, NatsKVConfig original, NewBucketOptions? seed)
    {
        var dialog = new CreateBucketDialog(seed ?? ToNewBucketOptions(item, original), isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryEditBucketAsync(item, original, options);
    }

    private static NewBucketOptions ToNewBucketOptions(KvBucketItem item, NatsKVConfig original) =>
        new(
            item.Name,
            original.Storage,
            (int)original.History,
            original.MaxAge == TimeSpan.Zero ? null : original.MaxAge,
            original.LimitMarkerTTL == TimeSpan.Zero ? null : original.LimitMarkerTTL);

    // NatsKVStatus (v2.8.2) carries no NatsKVConfig of its own - only Info.Config (the underlying
    // stream's StreamConfig) plus a top-level LimitMarkerTTL. This reconstructs the NatsKVConfig
    // UpdateStoreAsync needs field-by-field from those two sources, so every field
    // CreateBucketDialog doesn't expose (description, max value size, max bytes, replica count,
    // compression, republish, placement, mirror/sources, metadata, ...) round-trips unchanged -
    // per design.md Decision 2's "merge onto the live-fetched config, never reconstruct from
    // scratch" rule. Storage/Compression need an explicit mapping since NatsKVConfig and
    // StreamConfig use different (but same-shaped) types for them; Placement/Mirror/Sources/
    // Metadata share identical types across both configs, so those are copied as-is.
    private static NatsKVConfig ToNatsKVConfig(KvBucketItem item)
    {
        var status = item.Status;
        var config = status.Info.Config;
        return new NatsKVConfig(item.Name) {
            Description = config.Description,
            MaxValueSize = config.MaxMsgSize,
            History = config.MaxMsgsPerSubject,
            MaxAge = config.MaxAge,
            MaxBytes = config.MaxBytes,
            Storage = config.Storage == StreamConfigStorage.Memory ? NatsKVStorageType.Memory : NatsKVStorageType.File,
            NumberOfReplicas = config.NumReplicas,
            Republish = config.Republish is { } republish
                ? new NatsKVRepublish { Src = republish.Src, Dest = republish.Dest, HeadersOnly = republish.HeadersOnly }
                : null,
            Placement = config.Placement,
            Compression = config.Compression != StreamConfigCompression.None,
            Mirror = config.Mirror,
            Sources = config.Sources,
            Metadata = config.Metadata,
            LimitMarkerTTL = status.LimitMarkerTTL,
        };
    }

    // Merges onto `original` rather than calling `edited.ToNatsKVConfig()` - per design.md
    // Decision 2. Goes through the real KV-level `UpdateStoreAsync`, not a raw stream update
    // (unlike ObjectsTab's OBJ-only workaround - see design.md's KV-vs-OBJ asymmetry risk note).
    private async Task TryEditBucketAsync(KvBucketItem item, NatsKVConfig original, NewBucketOptions edited)
    {
        try {
            var updated = original with {
                History = edited.History ?? 1,
                MaxAge = edited.MaxAge ?? TimeSpan.Zero,
                LimitMarkerTTL = edited.LimitMarkerTTL ?? TimeSpan.Zero,
            };
            await _kv.UpdateStoreAsync(updated);
            _ = RefreshListAsync(edited.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Edit Bucket Failed ", ex.Message.Pad(), "_Ok");
                OpenEditBucketDialog(item, original, edited);
            });
        }
    }

    // Scoped by _listView.SelectedBucket alone - no _currentBucket check needed, since Ctrl+D is
    // only bound at the bucket level (see BucketListView) and this is unreachable from the key
    // level. Mirrors StreamsTab.TryDeleteStreamAsync exactly.
    private async Task TryDeleteBucketAsync()
    {
        if (_listView.SelectedBucket is not { } item) return;
        var name = item.Name;

        var choice = MessageBox.Query(
            App!, " Delete Bucket ",
            $"Delete bucket '{name}'? This cannot be undone.".Pad(),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _listView.NeighborIdentity(name);

        try {
            await _kv.DeleteStoreAsync(name);
            _ = RefreshListAsync(neighborName);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Delete Bucket Failed ", ex.Message.Pad(), "_Ok"));
        }
    }

    // GetStatusesAsync() returns every JetStream stream on the server, not just KV buckets - see
    // BucketName's comment.
    private async Task<IList<KvBucketItem>> FetchBucketsAsync() =>
        await _kv.GetStatusesAsync().ToObservable()
            .Select(status => (Name: status.Info.Config.TryGetKvBucketName(), Status: status))
            .Where(x => x.Name is not null)
            .Select(x => new KvBucketItem(x.Name!, x.Status))
            .ToList();

    private static async Task<(IList<string> Keys, bool Truncated)> FetchKeysAsync(INatsKVStore store, NatsFilter filter)
    {
        var observable = store.GetKeysAsync([filter.Native]).ToObservable();
        if (!filter.NativeFilterIsExact) observable = observable.Where(key => filter.Regex.IsMatch(key));
        var fetched = await observable.Take(KeyFilterCap + 1).ToList();
        var truncated = fetched.Count > KeyFilterCap;
        return (truncated ? fetched.Take(KeyFilterCap).ToList() : fetched, truncated);
    }

    // `selectName` highlights a specific bucket after the refresh (used right after a create, so
    // the new bucket is selected instead of ReplaceItems' default "keep whatever was highlighted
    // before" fallback) - null for a plain Ctrl+R/initial-load refresh.
    private async Task RefreshListAsync(string? selectName = null)
    {
        try {
            var buckets = await FetchBucketsAsync();
            App?.Invoke(() => _listView.ReplaceItems(buckets, selectName));
        } catch (Exception ex) {
            // Keep whatever the list previously showed rather than clearing it on a transient
            // error - matches StreamsTab's "poll or refresh error" handling.
            App?.Invoke(() => StatusChanged?.Invoke($"Values: {ex.Message}"));
        }
    }

    // `selectName` mirrors RefreshListAsync's own parameter - highlights a specific key after the
    // refresh (used right after a create/edit) instead of ReplaceItems' default "keep whatever was
    // highlighted before" fallback. Reads `_keyListView.ActiveFilter` directly rather than taking
    // it as a parameter, so Ctrl+R and post-create/edit refreshes stay scoped to the active filter
    // (if any) for free. No active filter compiles down to `>` (matches every key) - see
    // FetchKeysAsync.
    private async Task RefreshKeyListAsync(string? selectName = null)
    {
        if (_currentBucket is not { } bucket) return;

        try {
            var store = await _kv.GetStoreAsync(bucket);
            var filter = FilterExpression.TryCompile(_keyListView.ActiveFilter ?? ">")!;
            var (keys, truncated) = await FetchKeysAsync(store, filter);

            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into bucket.
                if (_currentBucket != bucket) return;
                _keyListTruncated = truncated;
                _keyListView.ReplaceItems(keys, selectName);
                UpdateKeyListTitle();
            });
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"Values: {ex.Message}"));
        }
    }

    // Reflects the active filter (if any) in the key list's title - the only UI surface
    // distinguishing "showing every key" from "showing a scoped subset", and (once a filtered
    // fetch has actually completed) whether that subset was truncated at KeyFilterCap - a
    // distinct indicator alongside, not replacing, the plain filter indicator. No-op at the
    // bucket level (_currentBucket is null).
    private void UpdateKeyListTitle()
    {
        if (_currentBucket is not { } bucket) return;
        _listLabel.Text = (_keyListView.ActiveFilter, _keyListTruncated) switch {
            ({ } pattern, true) => $"Keys of {bucket} (filter: {pattern}, truncated at {KeyFilterCap})",
            ({ } pattern, false) => $"Keys of {bucket} (filter: {pattern})",
            (null, true) => $"Keys of {bucket} (truncated at {KeyFilterCap})",
            (null, false) => $"Keys of {bucket}",
        };
    }

    // Always runs on the UI thread - mirrors OpenCreateBucketDialog exactly. Scoped by
    // _currentBucket alone since Ctrl+N is only bound at the key level (see KeyListView),
    // unreachable from the bucket level.
    private void OpenCreateKeyDialog(NewKeyOptions? seed)
    {
        if (_currentBucket is not { } bucket) return;

        var dialog = new CreateKeyDialog(seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryCreateKeyAsync(bucket, options);
    }

    private async Task TryCreateKeyAsync(string bucket, NewKeyOptions options)
    {
        try {
            var store = await _kv.GetStoreAsync(bucket);
            await store.PutAsync(options.Name, Encoding.UTF8.GetBytes(options.Value));
            _ = RefreshKeyListAsync(options.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Create Key Failed ", ex.Message.Pad(), "_Ok");
                OpenCreateKeyDialog(options);
            });
        }
    }

    // Ctrl+E at the key level. Per design.md's "Printable-text guard" decision, this always
    // re-fetches the key's current entry - never reuses whatever KeyDetails last polled - and
    // refuses to open the dialog for a missing or non-printable-text value.
    private void OpenEditKeyDialog()
    {
        if (_currentBucket is not { } bucket) return;
        if (_keyListView.SelectedKey is not { } key) return;

        _ = TryOpenEditKeyDialogAsync(bucket, key);
    }

    private async Task TryOpenEditKeyDialogAsync(string bucket, string key)
    {
        try {
            var store = await _kv.GetStoreAsync(bucket);
            var result = await store.TryGetEntryAsync<byte[]>(key);
            if (!result.Success) {
                App?.Invoke(() => StatusChanged?.Invoke($"Values: Key '{key}' no longer exists"));
                return;
            }

            if (!ValueText.TryDecode(result.Value.Value ?? [], out var text)) {
                App?.Invoke(() => StatusChanged?.Invoke($"Values: Cannot edit '{key}' — value is not printable text"));
                return;
            }

            App?.Invoke(() => OpenEditKeyDialog(bucket, key, new NewKeyOptions(key, text)));
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"Values: {ex.Message}"));
        }
    }

    // `options` reopens with the previously-entered Value after a failed edit (mirrors
    // OpenEditBucketDialog(status, original, seed)) - never re-fetches or re-checks
    // printability on a retry, since `options` is already known-good typed text.
    private void OpenEditKeyDialog(string bucket, string key, NewKeyOptions options)
    {
        var dialog = new CreateKeyDialog(options, isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } edited) _ = TryEditKeyAsync(bucket, key, edited);
    }

    private async Task TryEditKeyAsync(string bucket, string key, NewKeyOptions edited)
    {
        try {
            var store = await _kv.GetStoreAsync(bucket);
            await store.PutAsync(key, Encoding.UTF8.GetBytes(edited.Value));
            _ = RefreshKeyListAsync(key);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Edit Key Failed ", ex.Message.Pad(), "_Ok");
                OpenEditKeyDialog(bucket, key, edited);
            });
        }
    }

    // Scoped by _currentBucket and _keyListView.SelectedKey alone - Ctrl+D is only bound at the
    // key level (see KeyListView), unreachable from the bucket level. Mirrors
    // TryDeleteBucketAsync exactly, including the "cannot be undone" wording, even though the
    // underlying DeleteAsync is a tombstoning delete rather than a history-purging one - see
    // design.md's "Delete uses DeleteAsync" decision.
    private async Task TryDeleteKeyAsync()
    {
        if (_currentBucket is not { } bucket) return;
        if (_keyListView.SelectedKey is not { } key) return;

        var choice = MessageBox.Query(
            App!, " Delete Key ",
            $"Delete key '{key}'? This cannot be undone.".Pad(),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborKey = _keyListView.NeighborIdentity(key);

        try {
            var store = await _kv.GetStoreAsync(bucket);
            await store.DeleteAsync(key);
            _ = RefreshKeyListAsync(neighborKey);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, " Delete Key Failed ", ex.Message.Pad(), "_Ok"));
        }
    }
}
