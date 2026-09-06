using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.ObjectStore;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.ObjStore;

// Two levels (bucket list / object list) sharing one screen region: rather than tearing down and
// rebuilding views on every descend/ascend, both levels' EditFrame+list+details are built once
// and kept alive, toggled via Visible (EditFrame wraps a fixed child, so it can't itself swap
// between BucketListView/ObjectListView - see KvTab, which this deliberately mirrors).
// _currentBucket is null at the bucket level, and holds the drilled-into bucket's name at the
// object level.
internal sealed class ObjTab: View
{
    private readonly INatsJSContext _jetStream;
    private readonly INatsObjContext _obj;

    private readonly ObservableCollection<StreamInfo> _items = [];
    private readonly BucketListView _listView;
    private readonly EditFrame _bucketListFrame;
    private readonly BucketDetails _details;

    private readonly ObservableCollection<string> _objectItems = [];
    private readonly ObjectListView _objectListView;
    private readonly EditFrame _objectListFrame;
    private readonly ObjectDetails _objectDetails;

    private readonly Label _listLabel;
    private readonly Label _detailsLabel;

    // Guards the one-time initial ListStreamsAsync call - the list is otherwise load-once +
    // Ctrl+R only, per nats-obj's "Manual Bucket List Refresh" requirement.
    private bool _loaded;

    // Null at the bucket level; the drilled-into bucket's name at the object level.
    private string? _currentBucket;

    public event Action<string>? StatusChanged;

    public ObjTab(INatsJSContext jetStream, INatsObjContext obj)
    {
        CanFocus = true;
        _jetStream = jetStream;
        _obj = obj;

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

        _objectListView = new ObjectListView(_objectItems) { Background = Theme.EditableBackground };
        _objectListFrame = new EditFrame(_objectListView) {
            X = 0, Y = 1, Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _objectListView.RefreshRequested += () => _ = RefreshObjectListAsync();
        _objectListView.HighlightChanged += OnObjectHighlightChanged;
        _objectListView.AscendRequested += Ascend;

        _detailsLabel = new Label { Text = "Details", X = Pos.Right(_bucketListFrame) + 1, Y = 0 };
        _details = new BucketDetails(_obj) { X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _details.Error += message => StatusChanged?.Invoke($"OBJ: {message}");

        _objectDetails = new ObjectDetails(_obj) {
            X = Pos.Right(_bucketListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false,
        };
        _objectDetails.Error += message => StatusChanged?.Invoke($"OBJ: {message}");

        Add(_listLabel, _bucketListFrame, _objectListFrame, _detailsLabel, _details, _objectDetails);
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
        // Clear before showing - otherwise whatever a *previous* descent left behind (a different
        // bucket's objects, or a stale highlight) would flash for a frame until the fetch below
        // resolves. Clearing the list cascades into clearing the details pane too, via the same
        // HighlightChanged wiring an empty Ctrl+R result already goes through.
        _objectListView.ReplaceItems([]);
        _listLabel.Text = $"Objects of {name}";
        _detailsLabel.Text = "Object Details";
        _bucketListFrame.Visible = false;
        _objectListFrame.Visible = true;
        _details.Visible = false;
        _objectDetails.Visible = true;

        _details.SetActive(false);
        _objectDetails.SetActive(HasFocus);

        _objectListView.SetFocus();
        _ = RefreshObjectListAsync();
    }

    // Esc/Backspace from the object level. Never re-fetches the bucket list - it just re-shows
    // BucketListView's already-loaded state (nats-obj's "Manual Bucket List Refresh" requirement:
    // the bucket list only ever changes via explicit Ctrl+R).
    private void Ascend()
    {
        _currentBucket = null;
        _listLabel.Text = "Buckets";
        _detailsLabel.Text = "Details";
        _objectListFrame.Visible = false;
        _bucketListFrame.Visible = true;
        _objectDetails.Visible = false;
        _details.Visible = true;

        _objectDetails.SetActive(false);
        _details.SetActive(HasFocus);

        _listView.SetFocus();
    }

    // Always runs on the UI thread - either directly from the Ctrl+N key command (already on the
    // UI thread) or via the App.Invoke below, reached from TryCreateBucketAsync's continuation
    // after an await. Mirrors KvTab.OpenCreateBucketDialog exactly - see its comment for the full
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

    // Scoped by _listView.SelectedBucket alone - no _currentBucket check needed, since Ctrl+D is
    // only bound at the bucket level (see BucketListView) and this is unreachable from the object
    // level. Mirrors KvTab.TryDeleteBucketAsync exactly.
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
            // error - matches KvTab's "poll or refresh error" handling.
            App?.Invoke(() => StatusChanged?.Invoke($"OBJ: {ex.Message}"));
        }
    }

    private async Task RefreshObjectListAsync()
    {
        if (_currentBucket is not { } bucket) return;

        try {
            var store = await _obj.GetObjectStoreAsync(bucket);
            var names = new List<string>();
            await foreach (var metadata in store.ListAsync(new NatsObjListOpts())) names.Add(metadata.Name);
            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into bucket.
                if (_currentBucket == bucket) _objectListView.ReplaceItems(names);
            });
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"OBJ: {ex.Message}"));
        }
    }
}
