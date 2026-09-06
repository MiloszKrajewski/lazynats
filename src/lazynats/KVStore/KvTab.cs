using System.Collections.ObjectModel;
using System.Text;
using lazynats.Components;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.KVStore;

// Two levels (bucket list / key list) sharing one screen region: rather than tearing down and
// rebuilding views on every descend/ascend, both levels' EditFrame+list+details are built once
// and kept alive, toggled via Visible (EditFrame wraps a fixed child, so it can't itself swap
// between BucketListView/KeyListView - see StreamsTab, which this deliberately mirrors).
// _currentBucket is null at the bucket level, and holds the drilled-into bucket's name at the key
// level.
internal sealed class KvTab: View
{
    private readonly INatsKVContext _kv;

    private readonly ObservableCollection<NatsKVStatus> _items = [];
    private readonly BucketListView _listView;
    private readonly EditFrame _bucketListFrame;
    private readonly BucketDetails _details;

    private readonly ObservableCollection<string> _keyItems = [];
    private readonly KeyListView _keyListView;
    private readonly EditFrame _keyListFrame;
    private readonly KeyDetails _keyDetails;

    private readonly Label _listLabel;
    private readonly Label _detailsLabel;

    // Guards the one-time initial GetStatusesAsync call - the list is otherwise load-once +
    // Ctrl+R only, per nats-kv's "Manual Bucket List Refresh" requirement.
    private bool _loaded;

    // Null at the bucket level; the drilled-into bucket's name at the key level.
    private string? _currentBucket;

    public event Action<string>? StatusChanged;

    public KvTab(INatsKVContext kv)
    {
        CanFocus = true;
        _kv = kv;

        _listLabel = new Label { Text = "Buckets", X = 0, Y = 0 };
        _listView = new BucketListView(_items) { Background = Theme.EditableBackground };
        _bucketListFrame = new EditFrame(_listView) {
            X = 0, Y = 1, Width = Dim.Percent(40), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _listView.RefreshRequested += () => _ = RefreshListAsync();
        _listView.HighlightChanged += OnBucketHighlightChanged;
        _listView.DescendRequested += Descend;
        _listView.CreateRequested += () => OpenCreateBucketDialog(null);
        _listView.DeleteRequested += () => _ = TryDeleteBucketAsync();
        _listView.EditRequested += OpenEditBucketDialog;

        _keyListView = new KeyListView(_keyItems) { Background = Theme.EditableBackground };
        _keyListFrame = new EditFrame(_keyListView) {
            X = 0, Y = 1, Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _keyListView.RefreshRequested += () => _ = RefreshKeyListAsync();
        _keyListView.HighlightChanged += OnKeyHighlightChanged;
        _keyListView.AscendRequested += Ascend;
        _keyListView.CreateRequested += () => OpenCreateKeyDialog(null);
        _keyListView.DeleteRequested += () => _ = TryDeleteKeyAsync();
        _keyListView.EditRequested += OpenEditKeyDialog;

        _detailsLabel = new Label { Text = "Details", X = Pos.Right(_bucketListFrame) + 1, Y = 0 };
        _details = new BucketDetails(_kv) { X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _details.Error += message => StatusChanged?.Invoke($"KV: {message}");

        _keyDetails = new KeyDetails(_kv) {
            X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false,
        };
        _keyDetails.Error += message => StatusChanged?.Invoke($"KV: {message}");

        Add(_listLabel, _bucketListFrame, _keyListFrame, _detailsLabel, _details, _keyDetails);
    }

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

    private void OnBucketHighlightChanged(NatsKVStatus? status)
    {
        _details.SetTarget(status is null ? null : BucketName.From(status));
        _details.Show(status);
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
        if (_listView.SelectedBucket is not { } status) return;

        var name = BucketName.From(status);
        _currentBucket = name;
        // Clear before showing - otherwise whatever a *previous* descent left behind (a different
        // bucket's keys, or a stale highlight) would flash for a frame until the fetch below
        // resolves. Clearing the list cascades into clearing the details pane too, via the same
        // HighlightChanged wiring an empty Ctrl+R result already goes through.
        _keyListView.ReplaceItems([]);
        _listLabel.Text = $"Keys of {name}";
        _detailsLabel.Text = "Key Details";
        _bucketListFrame.Visible = false;
        _keyListFrame.Visible = true;
        _details.Visible = false;
        _keyDetails.Visible = true;

        _details.SetActive(false);
        _keyDetails.SetActive(HasFocus);

        _keyListView.SetFocus();
        _ = RefreshKeyListAsync();
    }

    // Esc/Backspace from the key level. Never re-fetches the bucket list - it just re-shows
    // BucketListView's already-loaded state (nats-kv's "Manual Bucket List Refresh" requirement:
    // the bucket list only ever changes via explicit Ctrl+R).
    private void Ascend()
    {
        _currentBucket = null;
        _listLabel.Text = "Buckets";
        _detailsLabel.Text = "Details";
        _keyListFrame.Visible = false;
        _bucketListFrame.Visible = true;
        _keyDetails.Visible = false;
        _details.Visible = true;

        _keyDetails.SetActive(false);
        _details.SetActive(HasFocus);

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
                MessageBox.ErrorQuery(App!, DialogText.Pad("Create Bucket Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenCreateBucketDialog(options);
            });
        }
    }

    // Scoped by _listView.SelectedBucket alone, same as TryDeleteBucketAsync - Ctrl+E is only
    // bound at the bucket level (see BucketListView), unreachable from the key level.
    private void OpenEditBucketDialog()
    {
        if (_listView.SelectedBucket is { } status) OpenEditBucketDialog(status, ToNatsKVConfig(status), null);
    }

    // `original` is the freshly-fetched NatsKVConfig to merge edits onto later (built once here,
    // not re-derived from `seed` on a reopen-after-failure, so a rejected attempt still merges
    // against the real server-side config rather than the user's typed values). `seed` reopens
    // with the previously-entered values after a failed edit (design.md Decision 5); omitted on
    // the initial Ctrl+E, where the dialog is seeded straight from `original` instead.
    private void OpenEditBucketDialog(NatsKVStatus status, NatsKVConfig original, NewBucketOptions? seed)
    {
        var dialog = new CreateBucketDialog(seed ?? ToNewBucketOptions(status, original), isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryEditBucketAsync(status, original, options);
    }

    private static NewBucketOptions ToNewBucketOptions(NatsKVStatus status, NatsKVConfig original) =>
        new(
            BucketName.From(status),
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
    private static NatsKVConfig ToNatsKVConfig(NatsKVStatus status)
    {
        var config = status.Info.Config;
        return new NatsKVConfig(BucketName.From(status)) {
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
    // (unlike ObjTab's OBJ-only workaround - see design.md's KV-vs-OBJ asymmetry risk note).
    private async Task TryEditBucketAsync(NatsKVStatus status, NatsKVConfig original, NewBucketOptions edited)
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
                MessageBox.ErrorQuery(App!, DialogText.Pad("Edit Bucket Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenEditBucketDialog(status, original, edited);
            });
        }
    }

    // Scoped by _listView.SelectedBucket alone - no _currentBucket check needed, since Ctrl+D is
    // only bound at the bucket level (see BucketListView) and this is unreachable from the key
    // level. Mirrors StreamsTab.TryDeleteStreamAsync exactly.
    private async Task TryDeleteBucketAsync()
    {
        if (_listView.SelectedBucket is not { } status) return;
        var name = BucketName.From(status);

        var choice = MessageBox.Query(
            App!, DialogText.Pad("Delete Bucket"),
            DialogText.Pad($"Delete bucket '{name}'? This cannot be undone."),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _listView.NeighborIdentity(name);

        try {
            await _kv.DeleteStoreAsync(name);
            _ = RefreshListAsync(neighborName);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Bucket Failed"), DialogText.Pad(ex.Message), "_Ok"));
        }
    }

    // `selectName` highlights a specific bucket after the refresh (used right after a create, so
    // the new bucket is selected instead of ReplaceItems' default "keep whatever was highlighted
    // before" fallback) - null for a plain Ctrl+R/initial-load refresh.
    private async Task RefreshListAsync(string? selectName = null)
    {
        try {
            var buckets = new List<NatsKVStatus>();
            // GetStatusesAsync() returns every JetStream stream on the server, not just KV
            // buckets - see BucketName's comment.
            await foreach (var status in _kv.GetStatusesAsync())
                if (BucketName.IsKvStream(status.Info.Config.Name)) buckets.Add(status);
            App?.Invoke(() => _listView.ReplaceItems(buckets, selectName));
        } catch (Exception ex) {
            // Keep whatever the list previously showed rather than clearing it on a transient
            // error - matches StreamsTab's "poll or refresh error" handling.
            App?.Invoke(() => StatusChanged?.Invoke($"KV: {ex.Message}"));
        }
    }

    // `selectName` mirrors RefreshListAsync's own parameter - highlights a specific key after the
    // refresh (used right after a create/edit) instead of ReplaceItems' default "keep whatever was
    // highlighted before" fallback.
    private async Task RefreshKeyListAsync(string? selectName = null)
    {
        if (_currentBucket is not { } bucket) return;

        try {
            var store = await _kv.GetStoreAsync(bucket);
            var keys = new List<string>();
            await foreach (var key in store.GetKeysAsync()) keys.Add(key);
            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into bucket.
                if (_currentBucket == bucket) _keyListView.ReplaceItems(keys, selectName);
            });
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"KV: {ex.Message}"));
        }
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
                MessageBox.ErrorQuery(App!, DialogText.Pad("Create Key Failed"), DialogText.Pad(ex.Message), "_Ok");
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
                App?.Invoke(() => StatusChanged?.Invoke($"KV: Key '{key}' no longer exists"));
                return;
            }

            if (!ValueText.TryDecode(result.Value.Value ?? [], out var text)) {
                App?.Invoke(() => StatusChanged?.Invoke($"KV: Cannot edit '{key}' — value is not printable text"));
                return;
            }

            App?.Invoke(() => OpenEditKeyDialog(bucket, key, new NewKeyOptions(key, text)));
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"KV: {ex.Message}"));
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
                MessageBox.ErrorQuery(App!, DialogText.Pad("Edit Key Failed"), DialogText.Pad(ex.Message), "_Ok");
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
            App!, DialogText.Pad("Delete Key"),
            DialogText.Pad($"Delete key '{key}'? This cannot be undone."),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborKey = _keyListView.NeighborIdentity(key);

        try {
            var store = await _kv.GetStoreAsync(bucket);
            await store.DeleteAsync(key);
            _ = RefreshKeyListAsync(neighborKey);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Key Failed"), DialogText.Pad(ex.Message), "_Ok"));
        }
    }
}
