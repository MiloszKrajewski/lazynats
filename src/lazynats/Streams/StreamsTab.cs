using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Streams;

// Two levels (stream list / consumer list) sharing one screen region: rather than tearing down
// and rebuilding views on every descend/ascend, both levels' EditFrame+list+details are built
// once and kept alive, toggled via Visible (EditFrame wraps a fixed child, so it can't itself
// swap between StreamListView/ConsumerListView - see design.md decision 7). _currentStream is
// null at the stream level, and holds the drilled-into stream's name at the consumer level.
internal sealed class StreamsTab: View
{
    private readonly INatsJSContext _jetStream;

    private readonly ObservableCollection<StreamInfo> _items = [];
    private readonly StreamListView _listView;
    private readonly EditFrame _streamListFrame;
    private readonly StreamDetails _details;

    private readonly ObservableCollection<ConsumerInfo> _consumerItems = [];
    private readonly ConsumerListView _consumerListView;
    private readonly EditFrame _consumerListFrame;
    private readonly ConsumerDetails _consumerDetails;

    private readonly Label _listLabel;
    private readonly Label _detailsLabel;

    // Guards the one-time initial ListStreamsAsync call - the list is otherwise load-once +
    // Ctrl+R only, per design.md's "list is load-once + manual refresh" decision.
    private bool _loaded;

    // Null at the stream level; the drilled-into stream's name at the consumer level.
    private string? _currentStream;

    public event Action<string>? StatusChanged;

    public StreamsTab(INatsJSContext jetStream)
    {
        CanFocus = true;
        _jetStream = jetStream;

        _listLabel = new Label { Text = "Streams", X = 0, Y = 0 };
        _listView = new StreamListView(_items) { Background = Theme.EditableBackground };
        _streamListFrame = new EditFrame(_listView) {
            X = 0, Y = 1, Width = Dim.Percent(40), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _listView.RefreshRequested += () => _ = RefreshListAsync();
        _listView.HighlightChanged += OnStreamHighlightChanged;
        _listView.DescendRequested += Descend;
        _listView.CreateRequested += () => OpenCreateStreamDialog(null);
        _listView.DeleteRequested += () => _ = TryDeleteStreamAsync();

        _consumerListView = new ConsumerListView(_consumerItems) { Background = Theme.EditableBackground };
        _consumerListFrame = new EditFrame(_consumerListView) {
            X = 0, Y = 1, Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _consumerListView.RefreshRequested += () => _ = RefreshConsumerListAsync();
        _consumerListView.HighlightChanged += OnConsumerHighlightChanged;
        _consumerListView.AscendRequested += Ascend;
        _consumerListView.CreateRequested += () => OpenCreateConsumerDialog(null);
        _consumerListView.DeleteRequested += () => _ = TryDeleteConsumerAsync();

        _detailsLabel = new Label { Text = "Details", X = Pos.Right(_streamListFrame) + 1, Y = 0 };
        _details = new StreamDetails(_jetStream) { X = Pos.Right(_streamListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _details.Error += message => StatusChanged?.Invoke($"Streams: {message}");

        _consumerDetails = new ConsumerDetails(_jetStream) {
            X = Pos.Right(_streamListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false,
        };
        _consumerDetails.Error += message => StatusChanged?.Invoke($"Streams: {message}");

        Add(_listLabel, _streamListFrame, _consumerListFrame, _detailsLabel, _details, _consumerDetails);
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

        if (_currentStream is null) _details.SetActive(newHasFocus);
        else _consumerDetails.SetActive(newHasFocus);
    }

    private void OnStreamHighlightChanged(StreamInfo? stream)
    {
        _details.SetTarget(stream?.Config.Name);
        _details.Show(stream);
    }

    private void OnConsumerHighlightChanged(ConsumerInfo? consumer)
    {
        _consumerDetails.SetTarget(_currentStream, consumer?.Name);
        _consumerDetails.Show(consumer);
    }

    // Enter on a highlighted stream. Always fetches the consumer list fresh (nats-streams'
    // "Consumer List" requirement - no per-stream caching, even re-descending into the same
    // stream just left) and never re-fetches the stream list on the way in.
    private void Descend()
    {
        if (_listView.SelectedStream?.Config.Name is not { } name) return;

        _currentStream = name;
        // Clear before showing - otherwise whatever a *previous* descent left behind (a
        // different stream's consumers, or a stale highlight) would flash for a frame until the
        // fetch below resolves. Clearing the list cascades into clearing the details pane too,
        // via the same HighlightChanged wiring an empty Ctrl+R result already goes through.
        _consumerListView.ReplaceItems([]);
        _listLabel.Text = $"Consumers of {name}";
        _detailsLabel.Text = "Consumer Details";
        _streamListFrame.Visible = false;
        _consumerListFrame.Visible = true;
        _details.Visible = false;
        _consumerDetails.Visible = true;

        _details.SetActive(false);
        _consumerDetails.SetActive(HasFocus);

        _consumerListView.SetFocus();
        _ = RefreshConsumerListAsync();
    }

    // Esc/Backspace from the consumer level. Never re-fetches the stream list - it just re-shows
    // StreamListView's already-loaded state (nats-streams' "Manual List Refresh" requirement:
    // the stream list only ever changes via explicit Ctrl+R).
    private void Ascend()
    {
        _currentStream = null;
        _listLabel.Text = "Streams";
        _detailsLabel.Text = "Details";
        _consumerListFrame.Visible = false;
        _streamListFrame.Visible = true;
        _consumerDetails.Visible = false;
        _details.Visible = true;

        _consumerDetails.SetActive(false);
        _details.SetActive(HasFocus);

        _listView.SetFocus();
    }

    // Always runs on the UI thread - either directly from the Ctrl+N key command (already on the
    // UI thread) or via the App.Invoke below, reached from TryCreateStreamAsync's continuation
    // after an await. Terminal.Gui has no SynchronizationContext (see AsyncExtensions.cs), so that
    // continuation resumes on an arbitrary thread pool thread; App!.Run(dialog) is a blocking
    // modal pump that has to run on the UI thread, same as every other Ctrl+N/E/D dialog in the
    // app already does from inside a main-loop-driven callback - nesting it inside an App.Invoke
    // callback is no different.
    private void OpenCreateStreamDialog(NewStreamOptions? seed)
    {
        var dialog = new CreateStreamDialog(seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryCreateStreamAsync(options);
    }

    private async Task TryCreateStreamAsync(NewStreamOptions options)
    {
        try {
            await _jetStream.CreateStreamAsync(options.ToStreamConfig());
            _ = RefreshListAsync(options.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, DialogText.Pad("Create Stream Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenCreateStreamDialog(options);
            });
        }
    }

    // Scoped to _currentStream at call time - the consumer-level list is only reachable once
    // Descend() has set it, so no extra state is needed here (mirrors OpenCreateStreamDialog).
    private void OpenCreateConsumerDialog(NewConsumerOptions? seed)
    {
        if (_currentStream is not { } stream) return;

        var dialog = new CreateConsumerDialog(seed);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryCreateConsumerAsync(stream, options);
    }

    private async Task TryCreateConsumerAsync(string stream, NewConsumerOptions options)
    {
        try {
            await _jetStream.CreateConsumerAsync(stream, options.ToConsumerConfig());
            _ = RefreshConsumerListAsync(options.Name);
        } catch (Exception ex) {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, DialogText.Pad("Create Consumer Failed"), DialogText.Pad(ex.Message), "_Ok");
                OpenCreateConsumerDialog(options);
            });
        }
    }

    private async Task TryDeleteStreamAsync()
    {
        if (_listView.SelectedStream?.Config.Name is not { } name) return;

        var choice = MessageBox.Query(
            App!, DialogText.Pad("Delete Stream"),
            DialogText.Pad($"Delete stream '{name}'? This cannot be undone."),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _listView.NeighborIdentity(name);

        try {
            await _jetStream.DeleteStreamAsync(name);
            _ = RefreshListAsync(neighborName);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Stream Failed"), DialogText.Pad(ex.Message), "_Ok"));
        }
    }

    // Scoped to _currentStream at call time, same as TryCreateConsumerAsync - the consumer-level
    // list is only reachable once Descend() has set it.
    private async Task TryDeleteConsumerAsync()
    {
        if (_currentStream is not { } stream) return;
        if (_consumerListView.SelectedConsumer?.Name is not { } name) return;

        var choice = MessageBox.Query(
            App!, DialogText.Pad("Delete Consumer"),
            DialogText.Pad($"Delete consumer '{name}'? This cannot be undone."),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _consumerListView.NeighborIdentity(name);

        try {
            await _jetStream.DeleteConsumerAsync(stream, name);
            _ = RefreshConsumerListAsync(neighborName);
        } catch (Exception ex) {
            App?.Invoke(() => MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Consumer Failed"), DialogText.Pad(ex.Message), "_Ok"));
        }
    }

    // `selectName` highlights a specific stream after the refresh (used right after a create, so
    // the new stream is selected instead of ReplaceItems' default "keep whatever was highlighted
    // before" fallback) - null for a plain Ctrl+R/initial-load refresh.
    private async Task RefreshListAsync(string? selectName = null)
    {
        try {
            var streams = new List<StreamInfo>();
            await foreach (var stream in _jetStream.ListStreamsAsync()) streams.Add(stream.Info);
            App?.Invoke(() => _listView.ReplaceItems(streams, selectName));
        } catch (Exception ex) {
            // Keep whatever the list previously showed rather than clearing it on a transient
            // error - per the "poll or refresh error" decision in design.md.
            App?.Invoke(() => StatusChanged?.Invoke($"Streams: {ex.Message}"));
        }
    }

    // `selectName` highlights a specific consumer after the refresh (used right after a create,
    // so the new consumer is selected instead of ReplaceItems' default "keep whatever was
    // highlighted before" fallback) - null for a plain Ctrl+R/descend refresh.
    private async Task RefreshConsumerListAsync(string? selectName = null)
    {
        if (_currentStream is not { } stream) return;

        try {
            var consumers = new List<ConsumerInfo>();
            await foreach (var consumer in _jetStream.ListConsumersAsync(stream)) consumers.Add(consumer.Info);
            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into stream.
                if (_currentStream == stream) _consumerListView.ReplaceItems(consumers, selectName);
            });
        } catch (Exception ex) {
            App?.Invoke(() => StatusChanged?.Invoke($"Streams: {ex.Message}"));
        }
    }
}
