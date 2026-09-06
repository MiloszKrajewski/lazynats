using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using lazynats.Core;
using lazynats.Subscriptions;
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
//
// `_items` is the master set, entirely owned by this class once passed in; `_filtered` is a second,
// same-relative-order ObservableCollection<T> the ListView actually renders - identical to `_items`
// when neither quick-search nor the shared filter wiring are active, otherwise the subset matching
// both (as an AND). Both narrow by matching against each item's presenter-formatted text (there is
// no per-item identity accessor here, unlike DrillableListView<T>) - see AttachFilterBox/
// EnableFilter and openspec/changes/unify-list-filtering/design.md Decision 4.
internal abstract class ListEditorView<T>: View, IShortcutSource, ITabOperationsSource, IFilterable
{
    private readonly ObservableCollection<T> _items;
    private readonly ObservableCollection<T> _filtered;
    private readonly IValuePresenter<T> _presenter;
    private readonly PresenterListDataSource<T> _dataSource;
    private readonly ListView _listView;
    private readonly Label _emptyHintLabel;
    private readonly bool _bindSharedKeys;

    private FilterBox? _filterBox;
    private bool _filterEnabled;
    private string? _activeFilterPattern;
    private Regex? _activeFilterRegex;
    private string _quickSearchQuery = string.Empty;

    // `bindSharedKeys` defaults true for standalone/modal usage (e.g. HeaderEditorView inside
    // PublishDialog), where there is no owning tab to hoist Ctrl+N/E/D up to. A tab-hosted instance
    // (e.g. SubscriptionsView inside SubscribeTab) passes false and exposes TabOperations instead -
    // see openspec/specs/tab-scoped-list-shortcuts/spec.md.
    public ListEditorView(ObservableCollection<T> items, IValuePresenter<T> presenter, bool bindSharedKeys = true)
    {
        CanFocus = true;
        _items = items;
        _filtered = [..items];
        _presenter = presenter;
        _bindSharedKeys = bindSharedKeys;

        _dataSource = new PresenterListDataSource<T>(_filtered, presenter);
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
        // which child currently has focus - same rationale as PublishDialog's header editor. Only
        // when bindSharedKeys is true (standalone/modal usage); a tab-hosted instance leaves these
        // unbound and exposes TabOperations instead for its owning tab to bind/dispatch.
        AddCommand(Command.New, TryCreateItem);
        AddCommand(Command.Edit, TryEditItem);
        AddCommand(Command.DeleteAll, TryDeleteItem);
        if (_bindSharedKeys) {
            KeyBindings.Add(Key.N.WithCtrl, Command.New);
            KeyBindings.Add(Key.E.WithCtrl, Command.Edit);
            KeyBindings.Add(Key.D.WithCtrl, Command.DeleteAll);

            // Ctrl+F for the shared filter wiring (see EnableFilter) - only needed in the
            // standalone/modal case, mirroring Ctrl+N/E/D above: a tab-hosted instance has no
            // direct key binding for Filter either (see DrillableListView<T>.EnableFilter), relying
            // entirely on its owning tab's TabOperations-based dispatch instead. Command.FindNext
            // is unused elsewhere on this view - repurposed here, the same way ObjectListView
            // repurposes Command.Save for Ctrl+S.
            AddCommand(Command.FindNext, () => { OpenFilterDialog(); return true; });
            KeyBindings.Add(Key.F.WithCtrl, Command.FindNext);
        }

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

    // Re-derives `_filtered` from `_items` too (per whatever quick-search/filter state is
    // currently active - see RederiveFiltered) - a subclass's own Add/Replace/Delete, or a
    // wholesale external rebuild (e.g. SubscriptionsView.RefreshFromRegistry), all funnel through
    // here. Neither quick-search nor the sticky filter reset on an items change (unlike
    // DrillableListView<T>'s quick-search, which resets on its own wholesale ReplaceItems) - see
    // "The filter persists across an item-collection change".
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RederiveFiltered();
        UpdateEmptyHintVisibility();
        EnsureValidSelection();
    }

    // Also re-syncs the scheme (not just OnHasFocusChanged): a modal opened via TryCreate/TryEdit
    // (e.g. PatternDialog) doesn't reliably re-raise this component's own HasFocusChanged on close,
    // so without this, deleting back down to empty right after an add-via-modal could leave the
    // hint showing stale unfocused styling even though focus is still actually here. Deliberately
    // reflects `_items`, not `_filtered` - a filter/search that hides everything still means there
    // genuinely are items, so "No items - Ctrl+N to add one" would be misleading; a filtered-to-
    // empty list instead just renders as a blank list, matching DrillableListView<T>'s identical
    // choice.
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
        if (_filtered.Count == 0) return;

        if (SelectedIndex is null)
            _listView.SelectedItem = 0;
    }

    private int? SelectedIndex =>
        _listView.SelectedItem is { } index and >= 0 && index < _filtered.Count ? index : null;

    // Re-derives `_filtered` from `_items`, narrowed by the sticky filter wiring's active regex (if
    // any) AND the live quick-search query (if any) - both apply together, matching
    // DrillableListView<T>'s identical "combine as an AND" rule. Preserves `_items`' relative
    // (insertion) order - this base never sorts, unlike DrillableListView<T> - so narrowing with
    // `.Where()` alone, never reordering, is what this needs to do.
    private void RederiveFiltered()
    {
        IEnumerable<T> matches = _items;

        if (_activeFilterRegex is { } filterRegex)
            matches = matches.Where(item => filterRegex.IsMatch(_presenter.Format(item)));

        if (_quickSearchQuery.Length > 0) {
            var regex = _quickSearchQuery.FuzzyToRegex();
            matches = matches.Where(item => regex.IsMatch(_presenter.Format(item)));
        }

        _filtered.Clear();
        foreach (var item in matches) _filtered.Add(item);
    }

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

    // Resolves the selected *filtered* item back to its position in `_items` (a direct reference/
    // value `IndexOf`, not an identity lookup - see openspec/changes/unify-list-filtering/
    // design.md Decision 4) before calling the existing index-based Replace, so editing a
    // filtered-in item never affects the wrong one.
    private void TryEditItem()
    {
        if (SelectedIndex is not { } filteredIndex) return;
        var original = _filtered[filteredIndex];
        var index = _items.IndexOf(original);
        if (index < 0) return;

        if (TryEdit(original, out var result)) Replace(index, result);
    }

    // Same filtered-index-to-underlying-index resolution as TryEditItem.
    private void TryDeleteItem()
    {
        if (SelectedIndex is not { } filteredIndex) return;
        var index = _items.IndexOf(_filtered[filteredIndex]);
        if (index < 0) return;

        Delete(index);
    }

    // Links an externally-created, externally-positioned FilterBox to this list, so "/" focuses it
    // and its text fuzzy-filters the currently-loaded items live, in memory - same shape and
    // rationale as DrillableListView<T>.AttachFilterBox (see that method's own comment).
    public void AttachFilterBox(FilterBox box)
    {
        _filterBox = box;
        box.AttachTo(this);

        AddCommand(Command.Find, () => { box.Focus(); return true; });
        KeyBindings.Add(new Key('/'), Command.Find);

        AddCommand(Command.Up, () => { box.Focus(); return true; });
        KeyBindings.Add(Key.CursorUp, Command.Up);
    }

    void IFilterable.ApplyFilter(string query)
    {
        _quickSearchQuery = query;
        RederiveFiltered();
        EnsureValidSelection();
    }

    void IFilterable.FocusList() => _listView.SetFocus();

    FilterBox? IFilterable.AttachedFilterBox => _filterBox;

    void IFilterable.HandleEmptySearchEscape() { }

    // Opt-in shared "Filter" (Ctrl+F) shape - same sticky, `* ? >`-grammar pattern-filter
    // mechanics as DrillableListView<T>.EnableFilter, adapted to match against each item's
    // presenter-formatted text instead of an identity accessor (this base has none). Called from a
    // subclass's own constructor, same convention as DrillableListView<T>'s Enable* shapes.
    protected void EnableFilter() => _filterEnabled = true;

    // Overridable per item type so the filter dialog's title reads naturally; defaulted rather
    // than abstract so a forgotten override still shows something useful.
    protected virtual string FilterDialogTitle => "Filter";

    private void OpenFilterDialog()
    {
        var dialog = new PatternDialog(
            FilterDialogTitle, _activeFilterPattern ?? string.Empty, allowEmpty: true,
            validator: p => FilterExpression.TryCompile(p) is not null);
        App!.Run(dialog);
        if (dialog.Result is not { } pattern) return;

        _activeFilterPattern = pattern.Length == 0 ? null : pattern;
        _activeFilterRegex = _activeFilterPattern is { } p ? FilterExpression.TryCompile(p)!.Regex : null;
        RederiveFiltered();
        EnsureValidSelection();
    }

    // Only meaningful when bindSharedKeys is true - a tab-hosted instance's New/Edit/Delete/Filter
    // hints move to TabOperations below, for the owning tab to advertise instead (otherwise the
    // same hints would be collected twice - once here, once from the tab). Search stays here
    // unconditionally - like DrillableListView<T>, quick-search is always list-owned, never
    // tab-dispatched.
    public virtual IEnumerable<ShortcutHint> Shortcuts
    {
        get
        {
            IEnumerable<ShortcutHint> hints = _bindSharedKeys
                ? [
                    new ShortcutHint(Key.N.WithCtrl, "New", TryCreateItem),
                    new ShortcutHint(Key.E.WithCtrl, "Edit", TryEditItem),
                    new ShortcutHint(Key.D.WithCtrl, "Delete", TryDeleteItem),
                ]
                : [];
            if (_bindSharedKeys && _filterEnabled) hints = hints.Append(new ShortcutHint(Key.F.WithCtrl, "Filter", OpenFilterDialog));
            if (_filterBox is { } box) hints = hints.Append(new ShortcutHint(new Key('/'), "Search", box.Focus));
            return hints;
        }
    }

    public virtual IEnumerable<ShortcutHint> TabOperations
    {
        get
        {
            IEnumerable<ShortcutHint> hints = [
                new(Key.N.WithCtrl, "New", TryCreateItem),
                new(Key.E.WithCtrl, "Edit", TryEditItem),
                new(Key.D.WithCtrl, "Delete", TryDeleteItem),
            ];
            if (_filterEnabled) hints = hints.Append(new ShortcutHint(Key.F.WithCtrl, "Filter", OpenFilterDialog));
            return hints;
        }
    }

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
