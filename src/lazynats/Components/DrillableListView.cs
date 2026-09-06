using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using lazynats.Core;
using lazynats.Subscriptions;
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
//
// Items are always kept sorted ascending by GetIdentity (Ordinal) in `_items` - the master set,
// entirely owned by this class once passed in (see ReplaceItems). `_filtered` is a second,
// always-sorted-the-same-way ObservableCollection that PresenterListDataSource/ListView actually
// render: identical to `_items` when no FilterBox is attached or its field is empty, otherwise the
// subset matching the search query - see openspec/changes/add-drillable-list-search/design.md
// Decision 2.
internal abstract class DrillableListView<T>: View, IShortcutSource, ITabOperationsSource, IFilterable
{
    private readonly ObservableCollection<T> _items;
    private readonly ObservableCollection<T> _filtered;
    private readonly PresenterListDataSource<T> _dataSource;
    private readonly ListView _listView;
    private readonly Label _emptyHintLabel;

    public event Action? RefreshRequested;
    public event Action<T?>? HighlightChanged;

    // Raised only once the corresponding Enable* helper below has been called by a subclass - see
    // EnableDescend/EnableAscend/EnableCreate/EnableDelete.
    public event Action? DescendRequested;
    public event Action? AscendRequested;
    public event Action? CreateRequested;
    public event Action? DeleteRequested;
    public event Action? EditRequested;

    // Raised whenever the shared filter wiring's active pattern changes (set or cleared) - see
    // EnableFilter. Purely an optional hook: an owning tab MAY subscribe to additionally scope a
    // server-side fetch (only KV keys do today - see ValuesTab); this list's own in-memory
    // narrowing (ApplyFilterAndSelect) works whether anything subscribes.
    public event Action<string?>? FilterChanged;

    private bool _ascendEnabled;
    private bool _createEnabled;
    private bool _deleteEnabled;
    private bool _editEnabled;
    private bool _filterEnabled;

    private FilterBox? _filterBox;

    // The shared filter wiring's sticky pattern/regex (survives ReplaceItems, unlike
    // _quickSearchQuery below - see "Active Filter Persists Across a Refresh"), and the live
    // quick-search text (resets on every ReplaceItems) - both narrow `_filtered` together in
    // ApplyFilterAndSelect, combined as an AND when both are active.
    private string? _activeFilterPattern;
    private Regex? _activeFilterRegex;
    private string _quickSearchQuery = string.Empty;

    protected DrillableListView(ObservableCollection<T> items)
    {
        CanFocus = true;
        _items = items;
        _filtered = [];

        _dataSource = new PresenterListDataSource<T>(_filtered, Presenter);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        // Disables ListView's own built-in type-to-jump navigation - AttachFilterBox's field is
        // this class's own answer to "find an item by typing", so the two must not compete for
        // the same keystrokes.
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

    // Exposed so a subclass can bind its own level-specific commands beyond the three shared
    // shapes below directly on the inner ListView, the same way those shapes bind Esc/Backspace/
    // Ctrl+N/Ctrl+D on this component itself via the inherited AddCommand/KeyBindings.
    protected ListView ListView => _listView;

    // Opt-in shared navigation shapes, called from a subclass constructor to activate exactly the
    // ones that subclass needs - independent of each other, so activating one has no effect on
    // whether another is active (see openspec/specs/drillable-list/spec.md's "Shared ... Wiring"
    // requirements). A subclass composes at most one of EnableDescend/EnableAscend, plus
    // optionally EnableCreate, EnableDelete, and/or EnableEdit, activatable independently of each
    // other. Quick-search is a separate opt-in, made by the owning Tab rather than the subclass
    // itself - see AttachFilterBox.

    // Enter -> DescendRequested.
    protected void EnableDescend() => _listView.Accepted += (_, _) => DescendRequested?.Invoke();

    // Esc/Backspace -> AscendRequested, plus an Esc "Back" Shortcuts hint.
    protected void EnableAscend()
    {
        _ascendEnabled = true;
        AddCommand(Command.Cancel, () => { AscendRequested?.Invoke(); return true; });
        KeyBindings.Add(Key.Esc, Command.Cancel);
        KeyBindings.Add(Key.Backspace, Command.Cancel);
    }

    // Ctrl+N -> CreateRequested, plus a "New" Shortcuts hint. Independent of EnableDelete.
    protected void EnableCreate()
    {
        _createEnabled = true;

        // The inner ListView's own DefaultKeyBindings alias Ctrl+N to Command.Down (Emacs-style
        // "next"), on top of the Down arrow key. It's the actual focus target, so left in place it
        // would consume Ctrl+N before this component's own binding below ever sees it. Down arrow
        // itself is untouched - only the redundant Ctrl+N alias for the same command is removed.
        _listView.KeyBindings.Remove(Key.N.WithCtrl);

        AddCommand(Command.New, () => { CreateRequested?.Invoke(); return true; });
    }

    // Ctrl+D -> DeleteRequested, plus a "Delete" Shortcuts hint. Independent of EnableCreate.
    protected void EnableDelete()
    {
        _deleteEnabled = true;

        AddCommand(Command.DeleteAll, () => { DeleteRequested?.Invoke(); return true; });
    }

    // Ctrl+E -> EditRequested, plus an "Edit" Shortcuts hint. Independent of EnableCreate/EnableDelete.
    protected void EnableEdit()
    {
        _editEnabled = true;

        AddCommand(Command.Edit, () => { EditRequested?.Invoke(); return true; });
    }

    // Ctrl+F -> opens a modal PatternDialog and, unlike Create/Delete/Edit, owns the whole
    // non-native filtering case end-to-end: compiling the pattern (shared `* ? >` grammar),
    // narrowing `_filtered` in memory, and persisting the pattern across ReplaceItems (see
    // ClearFilter). A subclass activating this needs no dialog code or event handler of its own -
    // only the one native-scoped consumer (ValuesTab, for KV keys) additionally subscribes to
    // FilterChanged to also scope its server-side fetch. See
    // openspec/changes/unify-list-filtering/design.md Decision 2.
    protected void EnableFilter() => _filterEnabled = true;

    // Overridable per item type so the filter dialog's title reads naturally (e.g. "Filter Keys");
    // defaulted rather than abstract so a forgotten override still shows something useful.
    protected virtual string FilterDialogTitle => "Filter";

    // The shared filter wiring's currently active pattern, or null if none - exposed for an owning
    // tab's title/header text (e.g. "Keys of bucket (filter: ...)").
    public string? ActiveFilter => _activeFilterPattern;

    private void OpenFilterDialog()
    {
        var dialog = new PatternDialog(
            FilterDialogTitle, _activeFilterPattern ?? string.Empty, allowEmpty: true,
            validator: p => FilterExpression.TryCompile(p) is not null);
        App!.Run(dialog);
        if (dialog.Result is not { } pattern) return;

        SetActiveFilter(pattern.Length == 0 ? null : pattern);
    }

    // Exposed for an owning tab to call on scope-changing navigation (e.g. descending into a
    // different bucket/stream) - the sticky filter otherwise survives ReplaceItems, so it only
    // clears via an explicit call like this one. Deliberately silent (raises no FilterChanged,
    // mirroring FilterBox.ResetSilently): a tab resetting scope already knows the filter is gone
    // and manages its own fetch itself, so a notification-driven re-fetch here would just be a
    // redundant network round trip - see ValuesTab.Descend/Ascend.
    public void ClearFilterSilently() => ApplyActiveFilter(null);

    private void SetActiveFilter(string? pattern)
    {
        ApplyActiveFilter(pattern);
        FilterChanged?.Invoke(pattern);
    }

    private void ApplyActiveFilter(string? pattern)
    {
        _activeFilterPattern = pattern;
        _activeFilterRegex = pattern is { } p ? FilterExpression.TryCompile(p)!.Regex : null;

        var previousIdentity = SelectedItem is { } current ? GetIdentity(current) : null;
        ApplyFilterAndSelect(previousIdentity);
        HighlightChanged?.Invoke(SelectedItem);
    }

    // Links an externally-created, externally-positioned FilterBox to this list, so "/" focuses it
    // and its text fuzzy-filters the currently-loaded items live, in memory - see
    // ApplyFilterAndSelect/FuzzyMatches and the IFilterable implementation below. Deliberately not
    // a View this class creates and embeds itself (that read as a frame within a frame, since this
    // whole component is already wrapped in its own EditFrame by the owning Tab) - the Tab
    // constructs and positions the box like any other View and links it here, per
    // openspec/changes/add-drillable-list-search/design.md.
    public void AttachFilterBox(FilterBox box)
    {
        _filterBox = box;
        box.AttachTo(this);

        AddCommand(Command.Find, () => { box.Focus(); return true; });
        KeyBindings.Add(new Key('/'), Command.Find);

        // Up-arrow at the top of the list (declined by the inner ListView itself, which only
        // handles Up when it can actually move the selection) moves to the attached FilterBox
        // directly - it sits immediately above the list on screen, so this is what "up" should do,
        // rather than bubbling out to the tab's own header via Terminal.Gui's generic
        // TabStop/AdvanceFocus handling. See design.md Decision 7.
        AddCommand(Command.Up, () => { box.Focus(); return true; });
        KeyBindings.Add(Key.CursorUp, Command.Up);
    }

    void IFilterable.ApplyFilter(string query)
    {
        var previousIdentity = SelectedItem is { } current ? GetIdentity(current) : null;
        _quickSearchQuery = query;
        ApplyFilterAndSelect(previousIdentity);
        HighlightChanged?.Invoke(SelectedItem);
    }

    void IFilterable.FocusList() => _listView.SetFocus();

    FilterBox? IFilterable.AttachedFilterBox => _filterBox;

    // Esc on the attached FilterBox while it was already empty - the same result as pressing Esc
    // directly on this list, per "Esc on an already-empty search field falls through to ascend".
    void IFilterable.HandleEmptySearchEscape()
    {
        if (_ascendEnabled) AscendRequested?.Invoke();
    }

    public T? SelectedItem =>
        _listView.SelectedItem is { } index and >= 0 && index < _filtered.Count ? _filtered[index] : default;

    // Re-fetched contents from a Ctrl+R (or the initial load) replace the master set wholesale,
    // sorted ascending by GetIdentity (Ordinal) - every subclass gets alphabetical order for free.
    // Any active search text is reset to empty (so a create/edit/refresh always leaves its result
    // visible regardless of what was previously typed), then the filtered projection is
    // re-derived and the previously-highlighted item's identity restored if still present,
    // otherwise the nearest remaining item by sort order - per "Identity-Preserving Replace".
    // `selectIdentity` overrides that fallback (e.g. after a create, to highlight the newly added
    // item instead of whatever was selected before it existed).
    public void ReplaceItems(IReadOnlyList<T> items, string? selectIdentity = null)
    {
        var previousIdentity = selectIdentity ?? (SelectedItem is { } current ? GetIdentity(current) : null);

        var sorted = items.OrderBy(GetIdentity, StringComparer.Ordinal);
        _items.Clear();
        foreach (var item in sorted) _items.Add(item);

        // Quick-search text resets on every refresh - clear the attached box's field (silently,
        // since ApplyFilterAndSelect below is the authoritative re-derive) rather than leave stale
        // text filtering out whatever this refresh just brought in (e.g. a just-created item).
        // The shared filter wiring's sticky pattern (_activeFilterPattern/_activeFilterRegex) is
        // deliberately left untouched here - it survives a refresh, unlike quick-search - see
        // "Active Filter Persists Across a Refresh".
        _filterBox?.ResetSilently();
        _quickSearchQuery = string.Empty;

        ApplyFilterAndSelect(previousIdentity);
        HighlightChanged?.Invoke(SelectedItem);
    }

    // Re-derives `_filtered` from the master set, narrowed by the sticky filter wiring's active
    // regex (if any) AND the live quick-search query (if any) - both apply together, per "Filter
    // and Quick-Search Combine When Both Are Active" - and selects `preferredIdentity` within it if
    // still present, otherwise the nearest remaining item by sort order (empty string sorts before
    // everything, so a null/absent `preferredIdentity` naturally lands on the first item - the same
    // "no previous selection" fallback ReplaceItems always had). Narrowing `_items` (already sorted
    // ascending) with `.Where()` alone, never reordering it, is what keeps a filtered/searched view
    // alphabetically ordered too - see design.md Decision 6.
    private void ApplyFilterAndSelect(string? preferredIdentity)
    {
        IEnumerable<T> matches = _items;

        if (_activeFilterRegex is { } filterRegex)
            matches = matches.Where(item => filterRegex.IsMatch(GetIdentity(item)));

        if (_quickSearchQuery.Length > 0) {
            // Built once per query change, reused across every item, rather than re-parsed per
            // item - see FuzzyExtensions.FuzzyToRegex.
            var regex = _quickSearchQuery.FuzzyToRegex();
            matches = matches.Where(item => regex.IsMatch(GetIdentity(item)));
        }

        _filtered.Clear();
        foreach (var item in matches) _filtered.Add(item);

        if (_filtered.Count == 0) {
            _listView.SelectedItem = null;
            return;
        }

        var index = preferredIdentity is not null ? IndexOfIdentity(_filtered, preferredIdentity) : -1;
        if (index < 0) index = IndexOfIdentity(_filtered, NearestIdentityCore(_filtered, preferredIdentity ?? string.Empty));
        _listView.SelectedItem = index;
    }

    private int IndexOfIdentity(ObservableCollection<T> items, string identity)
    {
        for (var i = 0; i < items.Count; i++)
            if (GetIdentity(items[i]) == identity) return i;

        return -1;
    }

    // Binary-search insertion-point lookup: the identity of the item in `items` (sorted ascending
    // by GetIdentity, Ordinal) nearest to `identity` by sort order - used as ReplaceItems'/
    // ApplyFilterAndSelect's fallback when `identity` itself isn't present. `items` must be
    // non-empty.
    private string NearestIdentityCore(ObservableCollection<T> items, string identity)
    {
        var lo = 0;
        var hi = items.Count;
        while (lo < hi) {
            var mid = lo + (hi - lo) / 2;
            if (string.CompareOrdinal(GetIdentity(items[mid]), identity) < 0) lo = mid + 1;
            else hi = mid;
        }

        return GetIdentity(items[lo < items.Count ? lo : items.Count - 1]);
    }

    // The identity of the nearest remaining item to `identity` by sort order within the currently
    // visible (filtered) collection, or null if it's empty - distinct from NeighborIdentity, which
    // looks up by list-position adjacency rather than sort order.
    public string? NearestIdentity(string identity) =>
        _filtered.Count == 0 ? null : NearestIdentityCore(_filtered, identity);

    // The identity of the item after `identity` in the current (filtered) list, or the one before
    // it if `identity` is last, or null if `identity` isn't present or the list would be emptied -
    // used by an owning tab to refocus a neighbor after deleting the item at `identity`.
    public string? NeighborIdentity(string identity)
    {
        var index = IndexOfIdentity(_filtered, identity);
        if (index < 0) return null;
        if (index + 1 < _filtered.Count) return GetIdentity(_filtered[index + 1]);
        return index - 1 >= 0 ? GetIdentity(_filtered[index - 1]) : null;
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

    // Back/Search - the two operations that stay genuinely list-owned once tab-scoped-list-shortcuts
    // moves Refresh/New/Delete/Edit up to the owning tab (see TabOperations below). A subclass with
    // its own list-local navigation commands beyond these still appends to this via
    // `base.Shortcuts.Append(...)` rather than replacing it.
    public virtual IEnumerable<ShortcutHint> Shortcuts
    {
        get
        {
            IEnumerable<ShortcutHint> hints = [];
            if (_ascendEnabled) hints = hints.Append(new ShortcutHint(Key.Esc, "Back", () => AscendRequested?.Invoke()));
            if (_filterBox is { } box) hints = hints.Append(new ShortcutHint(new Key('/'), "Search", box.Focus));
            return hints;
        }
    }

    // Refresh/New/Delete/Edit/Filter - whatever the enabled shared shapes (EnableCreate/
    // EnableDelete/EnableEdit/EnableFilter) imply, for the owning tab to bind Ctrl+R/N/D/E/F to and
    // dispatch through (see openspec/specs/tab-scoped-list-shortcuts/spec.md). A subclass with its
    // own tab-dispatched operation beyond these shapes still appends to this via
    // `base.TabOperations.Append(...)` rather than replacing it.
    public virtual IEnumerable<ShortcutHint> TabOperations =>
        new ShortcutHint?[] {
            new ShortcutHint(Key.R.WithCtrl, "Refresh", () => RefreshRequested?.Invoke()),
            _createEnabled ? new ShortcutHint(Key.N.WithCtrl, "New", () => CreateRequested?.Invoke()) : null,
            _deleteEnabled ? new ShortcutHint(Key.D.WithCtrl, "Delete", () => DeleteRequested?.Invoke()) : null,
            _editEnabled ? new ShortcutHint(Key.E.WithCtrl, "Edit", () => EditRequested?.Invoke()) : null,
            _filterEnabled ? new ShortcutHint(Key.F.WithCtrl, "Filter", OpenFilterDialog) : null,
        }.OfType<ShortcutHint>();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _dataSource.Dispose();
        base.Dispose(disposing);
    }
}
