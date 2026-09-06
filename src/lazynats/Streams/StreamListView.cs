using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Streams;

// Plain ListView-backed component, not a ListEditorView<T> subclass: per doc/stream-tab-UI.md
// this borrows ListEditorView's presenter-formatting/empty-hint conventions without inheriting
// its modal create/edit machinery, since there's no create/edit/delete here (nats-streams'
// "Read-Only List" requirement).
internal sealed class StreamListView: View, IShortcutSource
{
    private static readonly StreamNamePresenter Presenter = new();

    private readonly ObservableCollection<StreamInfo> _items;
    private readonly PresenterListDataSource<StreamInfo> _dataSource;
    private readonly ListView _listView;
    private readonly Label _emptyHintLabel;

    public event Action? RefreshRequested;
    public event Action? DescendRequested;
    public event Action<StreamInfo?>? HighlightChanged;

    public StreamListView(ObservableCollection<StreamInfo> items)
    {
        CanFocus = true;
        _items = items;

        _dataSource = new PresenterListDataSource<StreamInfo>(_items, Presenter);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;
        _listView.ValueChanged += (_, _) => HighlightChanged?.Invoke(SelectedStream);
        // ListView already binds Enter -> Command.Accept by default; re-raised here rather than
        // StreamsTab subscribing to the inner ListView directly, which is private.
        _listView.Accepted += (_, _) => DescendRequested?.Invoke();

        // Same "Label instead of a real focusable overlay" trick as ListEditorView's empty hint -
        // see that class for why CanFocus stays false here.
        _emptyHintLabel = new Label {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = false,
            Text = "No streams — Ctrl+R to refresh",
        };
        UpdateEmptyHintScheme();

        AddCommand(Command.Refresh, () => { RefreshRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.R.WithCtrl, Command.Refresh);

        Add(_listView, _emptyHintLabel);
        _items.CollectionChanged += (_, _) => UpdateEmptyHintVisibility();
        UpdateEmptyHintVisibility();
    }

    public StreamInfo? SelectedStream =>
        _listView.SelectedItem is { } index and >= 0 && index < _items.Count ? _items[index] : null;

    // Re-fetched contents from a Ctrl+R (or the initial load) replace _items wholesale; the
    // previously-highlighted stream stays highlighted if it's still present, otherwise the first
    // item is selected - per "Manual List Refresh"'s highlight-preservation/fallback scenarios.
    public void ReplaceItems(IReadOnlyList<StreamInfo> streams)
    {
        var currentName = SelectedStream?.Config.Name;

        _items.Clear();
        foreach (var stream in streams) _items.Add(stream);

        var index = currentName is null ? -1 : IndexOfName(currentName);
        _listView.SelectedItem = _items.Count == 0 ? null : index >= 0 ? index : 0;
        HighlightChanged?.Invoke(SelectedStream);
    }

    private int IndexOfName(string name)
    {
        for (var i = 0; i < _items.Count; i++)
            if (_items[i].Config.Name == name) return i;

        return -1;
    }

    private Color? _background;

    // Mirrors ListEditorView.Background - lets a caller (StreamsTab, pairing this with an
    // EditFrame) recolor the list rows and empty-hint overlay together.
    public Color? Background
    {
        get => _background;
        set => SetBackgroundColor(value);
    }

    private void SetBackgroundColor(Color? value)
    {
        _background = value;
        var foreground = _listView.GetAttributeForRole(VisualRole.Normal).Foreground;
        var scheme = value is { } background
            ? new Scheme(new Attribute(foreground, background))
            : null;
        _listView.SetScheme(scheme);
        UpdateEmptyHintScheme();
    }

    private void UpdateEmptyHintScheme()
    {
        var disabled = GetScheme().Disabled;
        var foreground = HasFocus ? new Color(255, 255, 255) : disabled.Foreground;
        var role = new Attribute(foreground, disabled.Background);
        var scheme = new Scheme(
            _background is { } background
                ? new Attribute(role.Foreground, background)
                : role);
        _emptyHintLabel.SetScheme(scheme);
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        UpdateEmptyHintScheme();
    }

    private void UpdateEmptyHintVisibility()
    {
        _emptyHintLabel.Visible = _items.Count == 0;
        UpdateEmptyHintScheme();
    }

    public IEnumerable<ShortcutHint> Shortcuts => [
        new(Key.R.WithCtrl, "Refresh", () => RefreshRequested?.Invoke()),
    ];

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dataSource.Dispose();
        base.Dispose(disposing);
    }
}
