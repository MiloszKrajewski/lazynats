using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

internal sealed class LiveUpdatesView: View
{
    private readonly ObservableCollection<string> _events = [];
    private readonly LiveLogDataSource _dataSource;
    private readonly ListView _listView;
    private readonly IDisposable _subscription;

    public event Action<string>? ItemSelected;

    public LiveUpdatesView(IObservable<string> source)
    {
        CanFocus = true;

        var divider = new Line { X = 0, Y = 0, Width = Dim.Fill(), Height = 1 };
        var heading = new Label { Text = "Live Updates", X = 0, Y = 1 };

        _dataSource = new LiveLogDataSource(_events);
        _listView = new ListView { X = 0, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;
        _listView.Accepted += OnAccepted;

        Add(divider, heading, _listView);

        AddCommand(Command.DeleteAll, () => { Clear(); return true; });
        KeyBindings.Add(Key.C, Command.DeleteAll);

        _subscription = source.Subscribe(e => App?.Invoke(() => OnEvent(e)));
    }

    public void Clear() => _events.Clear();

    private void OnEvent(string text)
    {
        var wasFollowing =
            _listView.SelectedItem is null ||
            _listView.SelectedItem == _events.Count - 1;
        _events.Add(text);
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
            _subscription.Dispose();
            _dataSource.Dispose();
        }
        base.Dispose(disposing);
    }
}
