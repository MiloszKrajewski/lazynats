using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

// Generalizes the "presenter-formatted list with New/Edit/Delete" shape duplicated between
// SubscriptionsView and PublishDialog's header editor. Row formatting is delegated to an injected
// presenter; New/Edit are delegated to abstract TryCreate/TryEdit callbacks so a subclass can run
// whatever modal its item type needs (a single field today, potentially several later) without
// this base class needing to know what that modal looks like. Obtaining a value (TryCreate/TryEdit)
// is kept separate from committing it (Add/Replace/Delete, all overridable with a default that
// mutates the item collection directly) so a subclass whose true source of truth lives elsewhere
// (e.g. SubscriptionsView redirecting into a NATS subscription registry) can commit there instead,
// without the base class also mutating the item collection on its behalf.
internal abstract class ListEditorView<T>: View, IShortcutSource
{
    private readonly ObservableCollection<T> _items;
    private readonly PresenterListDataSource<T> _dataSource;
    private readonly ListView _listView;
    private readonly Label _emptyHintLabel;

    public ListEditorView(ObservableCollection<T> items, IValuePresenter<T> presenter)
    {
        CanFocus = true;
        _items = items;

        _dataSource = new PresenterListDataSource<T>(_items, presenter);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.Source = _dataSource;

        // A separate overlay, not a fake row in _dataSource - stays outside the list's selection
        // model entirely, so SelectedIndex/Ctrl+N/E/D need no special-casing for it. Left
        // CanFocus = false (its default): a Terminal.Gui Label that actually holds keyboard focus
        // swallows all subsequent key input, which would break Ctrl+N/E/D and arrow-key tab
        // navigation while the list is empty. So focus highlighting is driven manually below
        // (UpdateEmptyHintScheme/OnHasFocusChanged) rather than via the framework's normal
        // per-view Normal/Focus role switching.
        _emptyHintLabel = new Label {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = false, Text = EmptyHint,
        };
        UpdateEmptyHintScheme();

        // Bound here (on the whole component), not on the list, so Ctrl+N/E/D work no matter
        // which child currently has focus - same rationale as PublishDialog's header editor.
        AddCommand(Command.New, TryCreateItem);
        AddCommand(Command.Edit, TryEditItem);
        AddCommand(Command.DeleteAll, TryDeleteItem);
        KeyBindings.Add(Key.N.WithCtrl, Command.New);
        KeyBindings.Add(Key.E.WithCtrl, Command.Edit);
        KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);

        Add(_listView, _emptyHintLabel);
        _items.CollectionChanged += OnItemsChanged;
        UpdateEmptyHintVisibility();
        EnsureValidSelection();
    }

    private void AddCommand(Command command, Action action) =>
        AddCommand(
            command, () => {
                action();
                return true;
            });

    // Overridden per item type so the hint reads naturally (e.g. "No subscriptions..." vs a
    // generic message); defaulted rather than abstract so a forgotten override still shows
    // something useful instead of nothing.
    protected virtual string EmptyHint => "No items — Ctrl+N to add one";

    private Color? _background;

    // Independent of any implicitly inherited scheme, so callers (e.g. EditFrame) can pair this
    // list visually with other edit controls. Unset (null) leaves prior inherited-scheme behavior
    // untouched. Applies to the empty-hint overlay too - it fully covers the list while empty, so
    // leaving it on its own unrelated (Disabled-role) background would defeat the point of setting
    // this in the first place.
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

    // Unfocused keeps the original dim look (this component's own Disabled role). Focused mirrors
    // what a real selected row in this list looks like. A selected ListView row gets there via an
    // inverted fg/bg bar, but Label - confirmed empirically - only ever paints its own foreground;
    // its background always shows through as whatever its container already painted, so an inverted
    // attribute here would render as invisible (dark-on-dark) rather than as a highlight bar. Bright
    // white foreground against that same unchanged background is the closest a Label can get to
    // "looks like the focused row."
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

    // HasFocus is recursively true here whenever _listView (the only real focus target) is
    // focused, so this is what drives UpdateEmptyHintScheme's focused/unfocused choice.
    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        UpdateEmptyHintScheme();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyHintVisibility();
        EnsureValidSelection();
    }

    // Also re-syncs the scheme (not just OnHasFocusChanged): a modal opened via TryCreate/TryEdit
    // (e.g. PatternDialog) doesn't reliably re-raise this component's own HasFocusChanged on close,
    // so without this, deleting back down to empty right after an add-via-modal could leave the
    // hint showing stale unfocused styling even though focus is still actually here.
    private void UpdateEmptyHintVisibility()
    {
        _emptyHintLabel.Visible = _items.Count == 0;
        UpdateEmptyHintScheme();
    }

    // A subclass whose true source of truth lives elsewhere (e.g. SubscriptionsView redirecting
    // into a NATS subscription registry) can rebuild _items wholesale - Terminal.Gui's ListView
    // resets SelectedItem to null on that kind of change and never re-selects anything on its
    // own, leaving a non-empty list with nothing highlighted and Ctrl+E/D silently inert.
    private void EnsureValidSelection()
    {
        if (_items.Count == 0) return;

        if (SelectedIndex is null)
            _listView.SelectedItem = 0;
    }

    private int? SelectedIndex =>
        _listView.SelectedItem is { } index and >= 0 && index < _items.Count ? index : null;

    // Run a modal appropriate to T and report whether the user committed a new value.
    protected abstract bool TryCreate(out T result);

    // Run a modal (seeded from `original`) appropriate to T and report whether the user committed
    // an edited value.
    protected abstract bool TryEdit(T original, out T result);

    protected virtual void Add(T value) => _items.Add(value);
    protected virtual void Replace(int index, T value) => _items[index] = value;
    protected virtual void Delete(int index) => _items.RemoveAt(index);

    private void TryCreateItem()
    {
        if (TryCreate(out var result)) Add(result);
    }

    private void TryEditItem()
    {
        if (SelectedIndex is not { } index) return;

        if (TryEdit(_items[index], out var result)) Replace(index, result);
    }

    private void TryDeleteItem()
    {
        if (SelectedIndex is not { } index) return;

        Delete(index);
    }

    public virtual IEnumerable<ShortcutHint> Shortcuts => [
        new(Key.N.WithCtrl, "New", TryCreateItem),
        new(Key.E.WithCtrl, "Edit", TryEditItem),
        new(Key.D.WithCtrl, "Delete", TryDeleteItem),
    ];

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _items.CollectionChanged -= OnItemsChanged;
            _dataSource.Dispose();
        }

        base.Dispose(disposing);
    }
}
