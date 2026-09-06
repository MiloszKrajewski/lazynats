using System.Collections.ObjectModel;
using System.Reactive.Linq;
using lazynats.Components;
using lazynats.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Streams;

// Two levels (stream list / consumer list) sharing one screen region: rather than tearing down
// and rebuilding views on every descend/ascend, both levels' EditFrame+list+details are built
// once and kept alive, toggled via Visible (EditFrame wraps a fixed child, so it can't itself
// swap between StreamListView/ConsumerListView - see design.md decision 7). _currentStream is
// null at the stream level, and holds the drilled-into stream's name at the consumer level.
internal sealed class StreamsTab: View, IShortcutSource
{
    private readonly INatsJSContext _jetStream;

    private readonly ObservableCollection<StreamInfo> _items = [];
    private readonly StreamListView _listView;
    private readonly FilterBox _streamFilterBox;
    private readonly EditFrame _streamListFrame;
    private readonly StreamDetails _details;

    private readonly ObservableCollection<ConsumerInfo> _consumerItems = [];
    private readonly ConsumerListView _consumerListView;
    private readonly FilterBox _consumerFilterBox;
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
        _streamFilterBox = new FilterBox { X = 0, Y = 1, Width = Dim.Percent(40) };
        _listView = new StreamListView(_items) { Background = Theme.EditableBackground };
        _listView.AttachFilterBox(_streamFilterBox);
        _streamListFrame = new EditFrame(_listView) {
            X = 0, Y = Pos.Bottom(_streamFilterBox), Width = Dim.Percent(40), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _listView.RefreshRequested += () => _ = RefreshListAsync();
        _listView.HighlightChanged += OnStreamHighlightChanged;
        _listView.DescendRequested += Descend;
        _listView.CreateRequested += () => OpenCreateStreamDialog(null);
        _listView.DeleteRequested += () => _ = TryDeleteStreamAsync();
        _listView.EditRequested += OpenEditStreamDialog;

        _consumerFilterBox = new FilterBox { X = 0, Y = 1, Width = Dim.Percent(40), Visible = false };
        _consumerListView = new ConsumerListView(_consumerItems) { Background = Theme.EditableBackground };
        _consumerListView.AttachFilterBox(_consumerFilterBox);
        _consumerListFrame = new EditFrame(_consumerListView) {
            X = 0, Y = Pos.Bottom(_consumerFilterBox), Width = Dim.Percent(40), Height = Dim.Fill(), Visible = false,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        _consumerListView.RefreshRequested += () => _ = RefreshConsumerListAsync();
        _consumerListView.HighlightChanged += OnConsumerHighlightChanged;
        _consumerListView.AscendRequested += Ascend;
        _consumerListView.CreateRequested += () => OpenCreateConsumerDialog(null);
        _consumerListView.DeleteRequested += () => _ = TryDeleteConsumerAsync();
        _consumerListView.EditRequested += OpenEditConsumerDialog;

        _detailsLabel = new Label { Text = "Details", X = Pos.Right(_streamListFrame) + 1, Y = 0 };
        _details = new StreamDetails(_jetStream)
            { X = Pos.Right(_streamListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _details.Error += message => StatusChanged?.Invoke($"Streams: {message}");

        _consumerDetails = new ConsumerDetails(_jetStream) {
            X = Pos.Right(_streamListFrame) + 1, Y = 2, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false,
        };
        _consumerDetails.Error += message => StatusChanged?.Invoke($"Streams: {message}");

        // Add()-order matches spatial top-down layout (label, then FilterBox, then its list) so
        // Tab/Shift+Tab cycles in reading order - ManagementTabs.FindFirstFocusableDescendant
        // separately skips FilterBox for the tab's *default* focus target, so entering/returning
        // to this tab still lands on the list, not the search field, despite that order.
        Add(
            _listLabel, _streamFilterBox, _streamListFrame, _consumerFilterBox, _consumerListFrame, _detailsLabel,
            _details, _consumerDetails);

        SetShortcutSource(_listView);
    }

    // Whichever list is currently the visible/active one - the stream list at the top level, the
    // consumer list once drilled in. Updated alongside every other piece of level-toggling state in
    // Descend/Ascend, not computed from _currentStream - see openspec/specs/tab-scoped-list-shortcuts/
    // spec.md. Backs both OnKeyDownNotHandled and Shortcuts below, so dispatch and advertisement read
    // the same list and can't drift apart.
    private ITabOperationsSource _shortcutSource = null!;

    private void SetShortcutSource(ITabOperationsSource source) => _shortcutSource = source;

    // No KeyBindings/AddCommand for Ctrl+R/N/D/E here - that would hardcode which keys this tab
    // forwards. Instead this fires once Terminal.Gui has already tried the focused view (and its own
    // ancestors, including whichever list is focused) and found no handler, at which point it's
    // this tab's turn; whatever key the currently active list's own TabOperations happens to expose
    // is what gets dispatched, so a list is free to add a new operation without this tab needing to
    // know about it in advance.
    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (_shortcutSource.TabOperations.FirstOrDefault(h => h.Key == key) is { Action: { } action })
        {
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

        if (newHasFocus && !_loaded)
        {
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
        _streamFilterBox.Visible = false;
        _streamListFrame.Visible = false;
        _consumerFilterBox.Visible = true;
        _consumerListFrame.Visible = true;
        _details.Visible = false;
        _consumerDetails.Visible = true;

        _details.SetActive(false);
        _consumerDetails.SetActive(HasFocus);
        SetShortcutSource(_consumerListView);

        _consumerListView.SetFocus();
        _ = RefreshConsumerListAsync();
    }

    // Esc/Backspace from the consumer level. Never re-fetches the stream list - it just re-shows
    // StreamListView's already-loaded state (nats-streams' "Manual List Refresh" requirement:
    // the stream list only ever changes via explicit Ctrl+R).
    private void Ascend()
    {
        _currentStream = null;
        // The consumer-level filter has no meaningful carry-over once back at the stream list -
        // see nats-streams' "Consumer List Filter" reset-on-ascend requirement. Silent: nothing
        // here needs to react to the clear (the consumer list is about to be hidden, not
        // re-fetched).
        _consumerListView.ClearFilterSilently();
        _listLabel.Text = "Streams";
        _detailsLabel.Text = "Details";
        _consumerFilterBox.Visible = false;
        _consumerListFrame.Visible = false;
        _streamFilterBox.Visible = true;
        _streamListFrame.Visible = true;
        _consumerDetails.Visible = false;
        _details.Visible = true;

        _consumerDetails.SetActive(false);
        _details.SetActive(HasFocus);
        SetShortcutSource(_listView);

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
        try
        {
            await _jetStream.CreateStreamAsync(options.ToStreamConfig());
            _ = RefreshListAsync(options.Name);
        }
        catch (Exception ex)
        {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Create Stream Failed ", ex.Message.Pad(), "_Ok");
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
        try
        {
            await _jetStream.CreateConsumerAsync(stream, options.ToConsumerConfig());
            _ = RefreshConsumerListAsync(options.Name);
        }
        catch (Exception ex)
        {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(
                    App!, " Create Consumer Failed ", ex.Message.Pad(), "_Ok");
                OpenCreateConsumerDialog(options);
            });
        }
    }

    // Scoped to the currently-highlighted stream at call time - re-fetching isn't needed since
    // `_listView.SelectedStream` already carries the full, current StreamInfo/Config. `seed`
    // reopens with the previously-entered values after a failed edit (design.md Decision 5);
    // omitted on the initial Ctrl+E, where the dialog is seeded straight from `original` instead.
    private void OpenEditStreamDialog(StreamConfig original, NewStreamOptions? seed = null)
    {
        var dialog = new CreateStreamDialog(seed ?? ToNewStreamOptions(original), isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryEditStreamAsync(original, options);
    }

    private void OpenEditStreamDialog()
    {
        if (_listView.SelectedStream?.Config is { } original) OpenEditStreamDialog(original);
    }

    private static NewStreamOptions ToNewStreamOptions(StreamConfig config) =>
        new(
            config.Name!,
            (config.Subjects ?? []).ToList(),
            config.Retention,
            config.MaxAge == TimeSpan.Zero ? null : config.MaxAge);

    // Merges onto `original` rather than calling `edited.ToStreamConfig()` - per design.md
    // Decision 2, that would silently reset every field this dialog doesn't expose (replica
    // count, limits, discard policy, description, metadata, ...) back to Create-time defaults.
    private async Task TryEditStreamAsync(StreamConfig original, NewStreamOptions edited)
    {
        try
        {
            var updated = original with {
                Subjects = edited.Subjects.ToList(), MaxAge = edited.MaxAge ?? TimeSpan.Zero
            };
            await _jetStream.UpdateStreamAsync(updated);
            _ = RefreshListAsync(edited.Name);
        }
        catch (Exception ex)
        {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Edit Stream Failed ", ex.Message.Pad(), "_Ok");
                OpenEditStreamDialog(original, edited);
            });
        }
    }

    // Scoped to _currentStream at call time, same as OpenCreateConsumerDialog.
    private void OpenEditConsumerDialog(string stream, ConsumerConfig original, NewConsumerOptions? seed = null)
    {
        var dialog = new CreateConsumerDialog(seed ?? ToNewConsumerOptions(original), isEdit: true);
        App!.Run(dialog);
        if (dialog.Result is { } options) _ = TryEditConsumerAsync(stream, original, options);
    }

    private void OpenEditConsumerDialog()
    {
        if (_currentStream is not { } stream) return;

        if (_consumerListView.SelectedConsumer?.Config is { } original) OpenEditConsumerDialog(stream, original);
    }

    private static NewConsumerOptions ToNewConsumerOptions(ConsumerConfig config) =>
        new(config.Name!, (config.FilterSubjects ?? []).ToList(), config.AckPolicy, config.DeliverPolicy);

    // Merges onto `original` rather than calling `edited.ToConsumerConfig()` - per design.md
    // Decision 2. The explicit `FilterSubject = null` clears a singular filter set outside
    // lazynats (design.md's "FilterSubject/FilterSubjects duality" decision) - this app always
    // writes the plural field, never the singular one.
    private async Task TryEditConsumerAsync(string stream, ConsumerConfig original, NewConsumerOptions edited)
    {
        try
        {
            var updated = original with {
                FilterSubjects = edited.FilterSubjects.Count > 0 ? edited.FilterSubjects.ToList() : null,
                FilterSubject = null,
            };
            await _jetStream.UpdateConsumerAsync(stream, updated);
            _ = RefreshConsumerListAsync(edited.Name);
        }
        catch (Exception ex)
        {
            App?.Invoke(() => {
                MessageBox.ErrorQuery(App!, " Edit Consumer Failed ", ex.Message.Pad(), "_Ok");
                OpenEditConsumerDialog(stream, original, edited);
            });
        }
    }

    private async Task TryDeleteStreamAsync()
    {
        if (_listView.SelectedStream?.Config.Name is not { } name) return;

        var choice = MessageBox.Query(
            App!, " Delete Stream ",
            $"Delete stream '{name}'? This cannot be undone.".Pad(),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _listView.NeighborIdentity(name);

        try
        {
            await _jetStream.DeleteStreamAsync(name);
            _ = RefreshListAsync(neighborName);
        }
        catch (Exception ex)
        {
            App?.Invoke(() => MessageBox.ErrorQuery(
                App!, " Delete Stream Failed ", ex.Message.Pad(), "_Ok"));
        }
    }

    // Scoped to _currentStream at call time, same as TryCreateConsumerAsync - the consumer-level
    // list is only reachable once Descend() has set it.
    private async Task TryDeleteConsumerAsync()
    {
        if (_currentStream is not { } stream) return;
        if (_consumerListView.SelectedConsumer?.Name is not { } name) return;

        var choice = MessageBox.Query(
            App!, " Delete Consumer ",
            $"Delete consumer '{name}'? This cannot be undone.".Pad(),
            "_Delete", "_Cancel");
        if (choice != 0) return;

        var neighborName = _consumerListView.NeighborIdentity(name);

        try
        {
            await _jetStream.DeleteConsumerAsync(stream, name);
            _ = RefreshConsumerListAsync(neighborName);
        }
        catch (Exception ex)
        {
            App?.Invoke(() => MessageBox.ErrorQuery(
                App!, " Delete Consumer Failed ", ex.Message.Pad(), "_Ok"));
        }
    }

    // Streams backing a KV/Object Store bucket are shown in their own dedicated tabs
    // (Values/Objects) instead - see nats-streams' "Stream List" requirement.
    private async Task<IList<StreamInfo>> FetchStreamsAsync() =>
        await _jetStream.ListStreamsAsync().ToObservable()
            .Where(IsRegularStream)
            .Select(stream => stream.Info)
            .ToList();

    private async Task<IList<ConsumerInfo>> FetchConsumersAsync(string stream) =>
        await _jetStream.ListConsumersAsync(stream).ToObservable()
            .Select(consumer => consumer.Info)
            .ToList();

    private static bool IsRegularStream(INatsJSStream stream) =>
        stream.Info.Config.TryGetKvBucketName() is null &&
        stream.Info.Config.TryGetObjBucketName() is null;

    // `selectName` highlights a specific stream after the refresh (used right after a create, so
    // the new stream is selected instead of ReplaceItems' default "keep whatever was highlighted
    // before" fallback) - null for a plain Ctrl+R/initial-load refresh.
    private async Task RefreshListAsync(string? selectName = null)
    {
        try
        {
            var streams = await FetchStreamsAsync();
            App?.Invoke(() => _listView.ReplaceItems(streams, selectName));
        }
        catch (Exception ex)
        {
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

        try
        {
            var consumers = await FetchConsumersAsync(stream);
            App?.Invoke(() => {
                // The user may have ascended back out while this was in flight - only apply a
                // result that's still for the currently-drilled-into stream.
                if (_currentStream == stream) _consumerListView.ReplaceItems(consumers, selectName);
            });
        }
        catch (Exception ex)
        {
            App?.Invoke(() => StatusChanged?.Invoke($"Streams: {ex.Message}"));
        }
    }
}
