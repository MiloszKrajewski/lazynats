using System.Collections.ObjectModel;
using System.Reactive.Linq;
using lazynats.Core;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.LiveFeed;

internal sealed class LiveUpdatesView: View
{
    private static readonly TimeSpan BufferWindow = TimeSpan.FromMilliseconds(25);

    private readonly ObservableCollection<FeedEnvelope> _events = [];
    private readonly LiveLogDataSource _dataSource;
    private readonly ListView _listView;
    private IDisposable? _subscription;

    public event Action<FeedEnvelope>? ItemSelected;

    // App resolves via the SuperView chain, so it's unavailable during the constructor (this
    // view has no SuperView yet - it's added to feedFrame/MainWindow afterward). Wiring the Rx
    // chain here instead, once Initialized fires, guarantees App is live.
    public LiveUpdatesView(IObservable<FeedEnvelope> feed, MessageDeduplicator dedup)
    {
        CanFocus = true;

        _dataSource = new LiveLogDataSource(_events);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;
        _listView.Accepted += OnAccepted;

        Add(_listView);

        AddCommand(Command.DeleteAll, () => { Clear(); return true; });
        KeyBindings.Add(Key.C, Command.DeleteAll);

        Initialized += (_, _) => {
            _subscription = feed
                .Where(envelope => !dedup.IsDuplicate(envelope))
                .Buffer(BufferWindow)
                .Where(batch => batch.Count > 0)
                .ObserveOnApp(App!)
                .Subscribe(batch => {
                    foreach (var envelope in batch) OnEvent(envelope);
                });
        };
    }

    public void Clear() => _events.Clear();

    private void OnEvent(FeedEnvelope envelope)
    {
        var wasFollowing =
            _listView.SelectedItem is null ||
            _listView.SelectedItem == _events.Count - 1;
        _events.Add(envelope);
        while (_events.Count > 128) _events.RemoveAt(0);
        if (wasFollowing) _listView.MoveEnd();
    }

    private void OnAccepted(object? sender, CommandEventArgs e)
    {
        if (_listView.SelectedItem is { } index and >= 0 && index < _events.Count)
            ItemSelected?.Invoke(_events[index]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            _subscription?.Dispose();
            _dataSource.Dispose();
        }
        base.Dispose(disposing);
    }
}
