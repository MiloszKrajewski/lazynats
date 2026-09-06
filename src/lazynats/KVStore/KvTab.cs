using System.Collections.ObjectModel;
using lazynats.Components;
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

        _keyListView = new KeyListView(_keyItems) { Background = Theme.EditableBackground };
        _keyListFrame = new EditFrame(_keyListView) {
            X = 0, Y = 1, Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _keyListView.RefreshRequested += () => _ = RefreshKeyListAsync();
        _keyListView.HighlightChanged += OnKeyHighlightChanged;
        _keyListView.AscendRequested += Ascend;

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

    private async Task RefreshKeyListAsync()
    {
        if (_currentBucket is not { } bucket) return;

        try {
            var store = await _kv.GetStoreAsync(bucket);
            var keys = new List<string>();
            await foreach (var key in store.GetKeysAsync()) keys.Add(key);
            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into bucket.
                if (_currentBucket == bucket) _keyListView.ReplaceItems(keys);
            });
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"KV: {ex.Message}"));
        }
    }
}
