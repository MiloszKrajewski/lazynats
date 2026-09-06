using System.Collections.ObjectModel;
using lazynats.Components;
using lazynats.Core;
using lazynats.Subscriptions;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.ObjectStore;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Objects;

// Two levels (bucket list / object list) sharing one screen region: rather than tearing down and
// rebuilding views on every descend/ascend, both levels' EditFrame+list+details are built once
// and kept alive, toggled via Visible (EditFrame wraps a fixed child, so it can't itself swap
// between BucketListView/ObjectListView - see ValuesTab, which this deliberately mirrors).
// _currentBucket is null at the bucket level, and holds the drilled-into bucket's name at the
// object level.
internal sealed class ObjectsTab: View, IShortcutSource
{
    private readonly INatsJSContext _jetStream;
    private readonly INatsObjContext _obj;

    private readonly ObservableCollection<StreamInfo> _items = [];
    private readonly BucketListView _listView;
    private readonly FilterBox _bucketFilterBox;
    private readonly EditFrame _bucketListFrame;
    private readonly BucketDetails _details;

    private readonly ObservableCollection<string> _objectItems = [];
    private readonly ObjectListView _objectListView;
    private readonly FilterBox _objectFilterBox;
    private readonly EditFrame _objectListFrame;
    private readonly ObjectDetails _objectDetails;

    private readonly Label _listLabel;
    private readonly Label _detailsLabel;

    // Guards the one-time initial ListStreamsAsync call - the list is otherwise load-once +
    // Ctrl+R only, per nats-obj's "Manual Bucket List Refresh" requirement.
    private bool _loaded;

    // Null at the bucket level; the drilled-into bucket's name at the object level.
    private string? _currentBucket;

    // Active post-fetch name filter pattern (filesystem-style wildcard) for the currently
    // drilled-into bucket, or null if unfiltered. Reset on both Descend and Ascend - a filter has
    // no meaningful carry-over to a different bucket, or back at the bucket list. Read directly by
    // RefreshObjectListAsync, so Ctrl+R and post-upload/delete refreshes stay narrowed for free.
    // Unlike KV, the fetch itself is never scoped - see
    // openspec/changes/add-obj-name-filter/design.md Decision 4.
    private string? _currentObjectFilter;

    public event Action<string>? StatusChanged;

    public ObjectsTab(INatsJSContext jetStream, INatsObjContext obj)
    {
        CanFocus = true;
        _jetStream = jetStream;
        _obj = obj;

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

        _objectFilterBox = new FilterBox { X = 0, Y = 1, Width = Dim.Percent(40), Visible = false };
        _objectListView = new ObjectListView(_objectItems) { Background = Theme.EditableBackground };
        _objectListView.AttachFilterBox(_objectFilterBox);
        _objectListFrame = new EditFrame(_objectListView) {
            X = 0, Y = Pos.Bottom(_objectFilterBox), Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _objectListView.RefreshRequested += () => _ = RefreshObjectListAsync();
        _objectListView.HighlightChanged += OnObjectHighlightChanged;
        _objectListView.AscendRequested += Ascend;
        _objectListView.CreateRequested += () => OpenUploadDialog(null);
        _objectListView.DownloadRequested += OpenDownloadDialog;
        _objectListView.DeleteRequested += () => _ = TryDeleteObjectAsync();
        _objectListView.FilterRequested += OpenObjectFilterDialog;

        _detailsLabel = new Label { Text = "Details", X = Pos.Right(_bucketListFrame) + 1, Y = 0 };
        _details = new BucketDetails(_obj) { X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _details.Error += message => StatusChanged?.Invoke($"Objects: {message}");

        _objectDetails = new ObjectDetails(_obj) {
            X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false,
        };
        _objectDetails.Error += message => StatusChanged?.Invoke($"Objects: {message}");

        // Add()-order matches spatial top-down layout (label, then FilterBox, then its list) so
        // Tab/Shift+Tab cycles in reading order - ManagementTabs.FindFirstFocusableDescendant
        // separately skips FilterBox for the tab's *default* focus target, so entering/returning
        // to this tab still lands on the list, not the search field, despite that order.
        Add(_listLabel, _bucketFilterBox, _bucketListFrame, _objectFilterBox, _objectListFrame, _detailsLabel, _details, _objectDetails);

        SetShortcutSource(_listView);
    }

    // Whichever list is currently the visible/active one - the bucket list at the top level, the
    // object list once drilled in. Updated alongside every other piece of level-toggling state in
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
    // know about it in advance. E.g. at the object level, ObjectListView's TabOperations has no
    // Ctrl+E entry (no Edit wiring), so Ctrl+E there simply falls through unhandled, even though the
    // bucket level's TabOperations does include one.
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
        else _objectDetails.SetActive(newHasFocus);
    }

    private void OnBucketHighlightChanged(StreamInfo? stream)
    {
        _details.SetTarget(stream is null ? null : BucketName.From(stream));
        // Unlike KV (whose bucket list already holds full NatsKVStatus), the OBJ bucket list only
        // carries StreamInfo - there's no cached NatsObjStatus to Show() instantly, so clear first.
        // SetTarget above already schedules a debounced fetch of the new bucket - see
        // PollingDetailsView's "Immediate Fetch On Demand".
        _details.Show(null);
    }

    private void OnObjectHighlightChanged(string? name)
    {
        _objectDetails.SetTarget(_currentBucket, name);
        // Unlike Stream/Consumer/Bucket, the object list only carries bare names - there's no
        // cached metadata to Show() instantly, so clear first (no stale flash of the previous
        // object's metadata). SetTarget above already schedules a debounced fetch of the new
        // object - see PollingDetailsView's "Immediate Fetch On Demand".
        _objectDetails.Show(null);
    }

    // Enter on a highlighted bucket. Always fetches the object list fresh (nats-obj's "Object
    // List" requirement - no per-bucket caching, even re-descending into the same bucket just
    // left) and never re-fetches the bucket list on the way in.
    private void Descend()
    {
        if (_listView.SelectedBucket is not { } stream) return;

        var name = BucketName.From(stream);
        _currentBucket = name;
        // A filter never carries over into a (possibly different) bucket entered by a fresh
        // descent - see "Post-Fetch Object Name Filter"'s reset-on-descend requirement.
        _currentObjectFilter = null;
        // Clear before showing - otherwise whatever a *previous* descent left behind (a different
        // bucket's objects, or a stale highlight) would flash for a frame until the fetch below
        // resolves. Clearing the list cascades into clearing the details pane too, via the same
        // HighlightChanged wiring an empty Ctrl+R result already goes through.
        _objectListView.ReplaceItems([]);
        UpdateObjectListTitle();
        _detailsLabel.Text = "Object Details";
        _bucketFilterBox.Visible = false;
        _bucketListFrame.Visible = false;
        _objectFilterBox.Visible = true;
        _objectListFrame.Visible = true;
        _details.Visible = false;
        _objectDetails.Visible = true;

        _details.SetActive(false);
        _objectDetails.SetActive(HasFocus);
        SetShortcutSource(_objectListView);

        _objectListView.SetFocus();
        _ = RefreshObjectListAsync();
    }

    // Esc/Backspace from the object level. Never re-fetches the bucket list - it just re-shows
    // BucketListView's already-loaded state (nats-obj's "Manual Bucket List Refresh" requirement:
    // the bucket list only ever changes via explicit Ctrl+R).
    private void Ascend()
    {
        _currentBucket = null;
        _currentObjectFilter = null;
        _listLabel.Text = "Buckets";
        _detailsLabel.Text = "Details";
        _objectFilterBox.Visible = false;
        _objectListFrame.Visible = false;
        _bucketFilterBox.Visible = true;
        _bucketListFrame.Visible = true;
        _objectDetails.Visible = false;
        _details.Visible = true;

        _objectDetails.SetActive(false);
        _details.SetActive(HasFocus);
        SetShortcutSource(_listView);

        _listView.SetFocus();
    }

    // Always runs on the UI thread - either directly from the Ctrl+N key command (already on the
    // UI thread) or via the App.Invoke below, reached from TryCreateBucketAsync's continuation
    // after an await. Mirrors ValuesTab.OpenCreateBucketDialog exactly - see its comment for the full
    // rationale.
    private void OpenCreateBucketDialog(NewBucketOptions? seed)
    {
        var dialog = new CreateBucketDialog(seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryCreateBucketAsync(options);
    }

    private async Task TryCreateBucketAsync(NewBucketOptions options)
    {
        try {
            await _obj.CreateObjectStoreAsync(options.ToNatsObjConfig());
            _ = RefreshListAsync(options.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, DialogText.Pad("Create Bucket Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenCreateBucketDialog(options);
            });
        }
    }

    // Scoped by _listView.SelectedBucket alone, same as TryDeleteBucketAsync - Ctrl+E is only
    // bound at the bucket level (see BucketListView), unreachable from the object level.
    private void OpenEditBucketDialog()
    {
        if (_listView.SelectedBucket is { } stream) OpenEditBucketDialog(stream.Config, ToNewBucketOptions(stream));
    }

    // `seed` carries the values to show - the freshly-fetched ones on the initial Ctrl+E, or the
    // previously-entered ones on a reopen after a failed edit (design.md Decision 5).
    private void OpenEditBucketDialog(StreamConfig original, NewBucketOptions seed)
    {
        var dialog = new CreateBucketDialog(seed, isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryEditBucketAsync(original, options);
    }

    private static NewBucketOptions ToNewBucketOptions(StreamInfo stream) =>
        new(BucketName.From(stream), stream.Config.MaxAge == TimeSpan.Zero ? null : stream.Config.MaxAge);

    // Merges onto `original` rather than calling `edited.ToNatsObjConfig()` - per design.md
    // Decision 2. No bucket-level update call exists on INatsObjContext (v2.8.2) - only
    // Create/Get/Delete plus object-level UpdateMetaAsync - so this goes through
    // INatsJSContext.UpdateStreamAsync directly against the underlying OBJ_<bucket> stream, the
    // same layer RefreshListAsync already reaches into for listing (design.md Decision 3). Scoped
    // to exactly the one field this dialog exposes (MaxAge), built from a `with`-copy of the
    // just-fetched StreamConfig, so every OBJ-internal invariant (subjects shape, discard policy,
    // rollup/delete-deny flags, ...) is preserved byte-for-byte - see design.md's risk note on
    // touching an OBJ bucket's backing stream directly.
    private async Task TryEditBucketAsync(StreamConfig original, NewBucketOptions edited)
    {
        try {
            var updated = original with { MaxAge = edited.MaxAge ?? TimeSpan.Zero };
            await _jetStream.UpdateStreamAsync(updated);
            _ = RefreshListAsync(edited.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, DialogText.Pad("Edit Bucket Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenEditBucketDialog(original, edited);
            });
        }
    }

    // Scoped by _listView.SelectedBucket alone - no _currentBucket check needed, since Ctrl+D is
    // only bound at the bucket level (see BucketListView) and this is unreachable from the object
    // level. Mirrors ValuesTab.TryDeleteBucketAsync exactly.
    private async Task TryDeleteBucketAsync()
    {
        if (_listView.SelectedBucket is not { } stream) return;
        var name = BucketName.From(stream);

        var choice = MessageBox.Query(
            App!, DialogText.Pad("Delete Bucket"),
            DialogText.Pad($"Delete bucket '{name}'? This cannot be undone."),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _listView.NeighborIdentity(name);

        try {
            await _obj.DeleteObjectStore(name, default);
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
            var buckets = new List<StreamInfo>();
            // ListStreamsAsync() returns every JetStream stream on the server, not just OBJ
            // buckets - see BucketName's comment.
            await foreach (var stream in _jetStream.ListStreamsAsync())
                if (BucketName.IsObjStream(stream.Info.Config.Name)) buckets.Add(stream.Info);
            App?.Invoke(() => _listView.ReplaceItems(buckets, selectName));
        } catch (Exception ex) {
            // Keep whatever the list previously showed rather than clearing it on a transient
            // error - matches ValuesTab's "poll or refresh error" handling.
            App?.Invoke(() => StatusChanged?.Invoke($"Objects: {ex.Message}"));
        }
    }

    // `selectName` mirrors ValuesTab.RefreshKeyListAsync's own parameter - highlights a specific
    // object after the refresh (used right after an upload) instead of ReplaceItems' default
    // "keep whatever was highlighted before" fallback. Always fetches every name from the server -
    // unlike KV, there's no server-side filter to scope this by (see design.md) - then, if
    // _currentObjectFilter is set, narrows the fetched result to matches before it ever reaches
    // ReplaceItems, so the list's own backing collection stays small even though the fetch itself
    // doesn't shrink.
    private async Task RefreshObjectListAsync(string? selectName = null)
    {
        if (_currentBucket is not { } bucket) return;

        try {
            var store = await _obj.GetObjectStoreAsync(bucket);
            var names = new List<string>();
            await foreach (var metadata in store.ListAsync(new NatsObjListOpts())) names.Add(metadata.Name);
            if (_currentObjectFilter is { } pattern) {
                var regex = pattern.WildcardToRegex();
                names = names.Where(name => regex.IsMatch(name)).ToList();
            }
            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into bucket.
                if (_currentBucket == bucket) _objectListView.ReplaceItems(names, selectName);
            });
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"Objects: {ex.Message}"));
        }
    }

    // Ctrl+F at the object level. PatternDialog's allowEmpty:true lets confirming an empty pattern
    // clear an active filter, unlike SubscriptionsView's use of the same dialog. Cancelling
    // (Result is null) leaves _currentObjectFilter and the list unchanged.
    private void OpenObjectFilterDialog()
    {
        if (_currentBucket is null) return;

        var dialog = new PatternDialog("Filter Objects", _currentObjectFilter ?? string.Empty, allowEmpty: true);
        App!.Run(dialog);
        if (dialog.Result is not { } pattern) return;

        _currentObjectFilter = pattern.Length == 0 ? null : pattern;
        UpdateObjectListTitle();
        _ = RefreshObjectListAsync();
    }

    // Reflects the active filter (if any) in the object list's title - the only UI surface
    // distinguishing "showing every object" from "showing a narrowed subset". No-op at the bucket
    // level (_currentBucket is null).
    private void UpdateObjectListTitle()
    {
        if (_currentBucket is not { } bucket) return;
        _listLabel.Text = _currentObjectFilter is { } pattern ? $"Objects of {bucket} (filter: {pattern})" : $"Objects of {bucket}";
    }

    // Always runs on the UI thread - mirrors OpenCreateBucketDialog exactly. Scoped by
    // _currentBucket alone since Ctrl+N is only bound at the object level (see ObjectListView),
    // unreachable from the bucket level.
    private void OpenUploadDialog(ObjectFileTransfer? seed)
    {
        if (_currentBucket is not { } bucket) return;

        var dialog = new ObjectFileDialog(isUpload: true, seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryUploadObjectAsync(bucket, options);
    }

    private async Task TryUploadObjectAsync(string bucket, ObjectFileTransfer options)
    {
        try {
            var store = await _obj.GetObjectStoreAsync(bucket);
            await using var stream = File.OpenRead(options.Path);
            await store.PutAsync(options.Key, stream, leaveOpen: false);
            _ = RefreshObjectListAsync(options.Key);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, DialogText.Pad("Upload Object Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenUploadDialog(options);
            });
        }
    }

    // Ctrl+S at the object level. Scoped by both _currentBucket and _objectListView.SelectedObject
    // - unreachable from the bucket level, and a no-op on an empty object list (no highlighted
    // object to download), per "Ctrl+S with no object highlighted does nothing".
    private void OpenDownloadDialog()
    {
        if (_currentBucket is not { } bucket) return;
        if (_objectListView.SelectedObject is not { } name) return;

        OpenDownloadDialog(bucket, new ObjectFileTransfer(name, string.Empty));
    }

    private void OpenDownloadDialog(string bucket, ObjectFileTransfer seed)
    {
        var dialog = new ObjectFileDialog(isUpload: false, seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryDownloadObjectAsync(bucket, options);
    }

    private async Task TryDownloadObjectAsync(string bucket, ObjectFileTransfer options)
    {
        try {
            var store = await _obj.GetObjectStoreAsync(bucket);
            await using var stream = File.Create(options.Path);
            await store.GetAsync(options.Key, stream, leaveOpen: false);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, DialogText.Pad("Download Object Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenDownloadDialog(bucket, options);
            });
        }
    }

    // Scoped by _currentBucket and _objectListView.SelectedObject alone - Ctrl+D is only bound at
    // the object level (see ObjectListView), unreachable from the bucket level. Mirrors
    // TryDeleteKeyAsync exactly.
    private async Task TryDeleteObjectAsync()
    {
        if (_currentBucket is not { } bucket) return;
        if (_objectListView.SelectedObject is not { } name) return;

        var choice = MessageBox.Query(
            App!, DialogText.Pad("Delete Object"),
            DialogText.Pad($"Delete object '{name}'? This cannot be undone."),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _objectListView.NeighborIdentity(name);

        try {
            var store = await _obj.GetObjectStoreAsync(bucket);
            await store.DeleteAsync(name);
            _ = RefreshObjectListAsync(neighborName);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Object Failed"), DialogText.Pad(ex.Message), "_Ok"));
        }
    }
}
