using System.Collections.ObjectModel;
using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Streams;

// Mirrors StreamListView deliberately closely (see openspec/changes/add-consumer-drilldown/
// design.md decision 1) - same plain-ListView-backed shape, same Ctrl+R refresh convention -
// plus the Esc/Backspace-to-ascend binding StreamListView doesn't need.
internal sealed class ConsumerListView: View, IShortcutSource
{
    private static readonly ConsumerNamePresenter Presenter = new();

    private readonly ObservableCollection<ConsumerInfo> _items;
    private readonly PresenterListDataSource<ConsumerInfo> _dataSource;
    private readonly ListView _listView;
    private readonly Label _emptyHintLabel;

    public event Action? RefreshRequested;
    public event Action? AscendRequested;
    public event Action<ConsumerInfo?>? HighlightChanged;

    public ConsumerListView(ObservableCollection<ConsumerInfo> items)
    {
        CanFocus = true;
        _items = items;

        _dataSource = new PresenterListDataSource<ConsumerInfo>(_items, Presenter);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;
        _listView.ValueChanged += (_, _) => HighlightChanged?.Invoke(SelectedConsumer);

        // Same "Label instead of a real focusable overlay" trick as ListEditorView's empty hint -
        // see that class for why CanFocus stays false here.
        _emptyHintLabel = new Label {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = false,
            Text = "No consumers — Ctrl+R to refresh",
        };
        UpdateEmptyHintScheme();

        AddCommand(Command.Refresh, () => { RefreshRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.R.WithCtrl, Command.Refresh);

        // No default binding exists for "ascend" the way Enter already means Command.Accept on
        // ListView - both keys map to the same Command.Cancel action.
        AddCommand(Command.Cancel, () => { AscendRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.Esc, Command.Cancel);
        KeyBindings.Add(Key.Backspace, Command.Cancel);

        Add(_listView, _emptyHintLabel);
        _items.CollectionChanged += (_, _) => UpdateEmptyHintVisibility();
        UpdateEmptyHintVisibility();
    }

    public ConsumerInfo? SelectedConsumer =>
        _listView.SelectedItem is { } index and >= 0 && index < _items.Count ? _items[index] : null;

    // Always a wholesale replace, never a per-stream cache: every descent (and every Ctrl+R)
    // re-fetches from scratch, per nats-streams' "Consumer List" requirement.
    public void ReplaceItems(IReadOnlyList<ConsumerInfo> consumers)
    {
        var currentName = SelectedConsumer?.Name;

        _items.Clear();
        foreach (var consumer in consumers) _items.Add(consumer);

        var index = currentName is null ? -1 : IndexOfName(currentName);
        _listView.SelectedItem = _items.Count == 0 ? null : index >= 0 ? index : 0;
        HighlightChanged?.Invoke(SelectedConsumer);
    }

    private int IndexOfName(string name)
    {
        for (var i = 0; i < _items.Count; i++)
            if (_items[i].Name == name) return i;

        return -1;
    }

    private Color? _background;

    // Mirrors StreamListView.Background - lets a caller (StreamsTab, pairing this with an
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
        new(Key.Esc, "Back", () => AscendRequested?.Invoke()),
    ];

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dataSource.Dispose();
        base.Dispose(disposing);
    }
}
