using System.Collections.ObjectModel;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

// Shared plumbing behind StreamListView/ConsumerListView (and the future KV/OBJ key/file lists):
// PresenterListDataSource/ListView wiring, empty-hint mechanics, Background, identity-preserving
// ReplaceItems, and the Ctrl+R -> RefreshRequested binding. Level-specific navigation (Enter ->
// descend, Esc/Backspace -> ascend, ...) is deliberately left to each subclass - list behavior is
// driven by list type, not item type. See
// openspec/changes/extract-drillable-list-base/design.md.
internal abstract class DrillableListView<T>: View, IShortcutSource
{
    private readonly ObservableCollection<T> _items;
    private readonly PresenterListDataSource<T> _dataSource;
    private readonly ListView _listView;
    private readonly Label _emptyHintLabel;

    public event Action? RefreshRequested;
    public event Action<T?>? HighlightChanged;

    protected DrillableListView(ObservableCollection<T> items)
    {
        CanFocus = true;
        _items = items;

        _dataSource = new PresenterListDataSource<T>(_items, Presenter);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;
        _listView.ValueChanged += (_, _) => HighlightChanged?.Invoke(SelectedItem);

        // Same "Label instead of a real focusable overlay" trick as ListEditorView's empty hint -
        // see that class for why CanFocus stays false here.
        _emptyHintLabel = new Label {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = false,
            Text = EmptyHintText,
        };
        UpdateEmptyHintScheme();

        AddCommand(Command.Refresh, () => { RefreshRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.R.WithCtrl, Command.Refresh);

        Add(_listView, _emptyHintLabel);
        _items.CollectionChanged += (_, _) => UpdateEmptyHintVisibility();
        UpdateEmptyHintVisibility();
    }

    // Required per item type. Back these with static state (as both existing subclasses already
    // do for their presenter) rather than instance state set via field initializer - this getter
    // runs from the base constructor, before a derived class's own field initializers have run.
    protected abstract IValuePresenter<T> Presenter { get; }
    protected abstract string EmptyHintText { get; }
    protected abstract string GetIdentity(T item);

    // Exposed so a subclass can bind its own level-specific commands (e.g. Enter -> descend) on
    // the inner ListView, the same way ConsumerListView binds Esc/Backspace on this component
    // itself via the inherited AddCommand/KeyBindings.
    protected ListView ListView => _listView;

    public T? SelectedItem =>
        _listView.SelectedItem is { } index and >= 0 && index < _items.Count ? _items[index] : default;

    // Re-fetched contents from a Ctrl+R (or the initial load) replace _items wholesale; the
    // previously-highlighted item stays highlighted if it's still present (by GetIdentity),
    // otherwise the first item is selected - per "Identity-Preserving Replace".
    public void ReplaceItems(IReadOnlyList<T> items)
    {
        var currentIdentity = SelectedItem is { } current ? GetIdentity(current) : null;

        _items.Clear();
        foreach (var item in items) _items.Add(item);

        var index = currentIdentity is null ? -1 : IndexOfIdentity(currentIdentity);
        _listView.SelectedItem = _items.Count == 0 ? null : index >= 0 ? index : 0;
        HighlightChanged?.Invoke(SelectedItem);
    }

    private int IndexOfIdentity(string identity)
    {
        for (var i = 0; i < _items.Count; i++)
            if (GetIdentity(_items[i]) == identity) return i;

        return -1;
    }

    private Color? _background;

    // Independent of any implicitly inherited scheme, applied to both the list's fill and the
    // empty-state hint overlay - mirrors ListEditorView.Background.
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

    // Just the Ctrl+R hint - a subclass with its own navigation commands (e.g. ConsumerListView's
    // Esc/Backspace) appends to this via `base.Shortcuts.Append(...)` rather than replacing it.
    public virtual IEnumerable<ShortcutHint> Shortcuts => [
        new(Key.R.WithCtrl, "Refresh", () => RefreshRequested?.Invoke()),
    ];

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dataSource.Dispose();
        base.Dispose(disposing);
    }
}
