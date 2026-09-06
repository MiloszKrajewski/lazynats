using System.Collections.ObjectModel;
using System.Threading.Channels;
using lazynats.LiveFeed;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

internal sealed class LiveUpdatesView: View
{
    private readonly ObservableCollection<FeedEnvelope> _events = [];
    private readonly LiveLogDataSource _dataSource;
    private readonly ListView _listView;
    private readonly CancellationTokenSource _cts = new();

    public event Action<FeedEnvelope>? ItemSelected;

    public LiveUpdatesView(ChannelReader<FeedEnvelope> feed, MessageDeduplicator dedup)
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

        var loop = new FeedReaderLoop(feed, dedup, OnBatch);
        _ = loop.RunAsync(_cts.Token);
    }

    public void Clear() => _events.Clear();

    private void OnBatch(IReadOnlyList<FeedEnvelope> batch) =>
        App?.Invoke(() => {
            foreach (var envelope in batch) OnEvent(envelope);
        });

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
            _cts.Cancel();
            _cts.Dispose();
            _dataSource.Dispose();
        }
        base.Dispose(disposing);
    }
}
