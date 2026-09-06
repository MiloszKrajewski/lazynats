using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Components;

// What a list needs to expose to an attached FilterBox - lets the two link to each other
// (FilterBox.AttachTo / DrillableListView<T>.AttachFilterBox) without either owning the other, so
// a Tab is free to construct and position each independently instead of one being nested inside
// the other's frame. See openspec/changes/add-drillable-list-search/design.md.
internal interface IFilterable
{
    // The field's current text, live, on every keystroke - re-derive whatever's currently
    // displayed from it. Never issues a fetch.
    void ApplyFilter(string query);

    // Return focus to the list - e.g. after the field's text is cleared via Esc.
    void FocusList();

    // Esc was pressed on the field while it was already empty - the list decides what that means
    // (e.g. ascend, if that shape is active), or nothing at all.
    void HandleEmptySearchEscape();

    // This list's own attached FilterBox, or null if none is attached - used by
    // ManagementTabs.AdvanceWithinPage to find what to toggle Tab/Shift+Tab to (see that method).
    FilterBox? AttachedFilterBox { get; }
}

// A persistent, single-line search field a Tab constructs and positions like any other View
// (never nested inside the attached list's own EditFrame - that reads as a frame within a frame)
// and links to a list via AttachTo, called by the list's own AttachFilterBox. See
// openspec/changes/add-drillable-list-search/design.md.
internal sealed class FilterBox: View
{
    private readonly TextField _field;
    private IFilterable? _target;
    private bool _suppressChange;
    private string _snapshot = string.Empty;

    public FilterBox()
    {
        // Not focusable until Activate() (bound to "/" on the attached list) sets this true -
        // Tab/Shift+Tab, arrow-key navigation, and a mouse click all consult a view's CanFocus
        // before landing on it, so this one flag closes off every entry path except "/" at once.
        // See openspec/changes/single-key-shortcuts/design.md's "FilterBox.CanFocus toggles around
        // activation" decision.
        CanFocus = false;
        Height = 3;

        _field = new TextField();
        _field.FixPasteRedraw();
        var frame = new EditFrame(_field) {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        Add(frame);

        _field.ValueChanged += (_, _) => {
            if (!_suppressChange) _target?.ApplyFilter(_field.Text);
        };

        // Enter: jump straight into the list without needing a second Tab/Down press - a
        // convenience mirroring "type a query, hit Enter to work with the results" elsewhere
        // (browser address bars, fzf, ...). Handled (not Accepted) and always marked e.Handled,
        // same as PatternDialog's own TextField - there's no button here to otherwise consume it.
        _field.Accepting += (_, e) => {
            e.Handled = true;
            _target?.FocusList();
        };

        // Esc: revert to the pre-activation snapshot (discarding whatever was typed this session)
        // and return focus to the list - unless the field is already empty AND so was the
        // snapshot, in which case there's nothing to revert and the attached list's own idea of
        // an empty-field Esc takes over instead - see IFilterable.HandleEmptySearchEscape. Esc
        // reaches ordinary key-preprocessing (KeyDown), so it's handled that way, unlike
        // Tab/Shift+Tab/Up/Down below.
        _field.KeyDown += (_, key) => {
            if (key != Key.Esc) return;
            key.Handled = true;
            if (_field.Text.Length == 0 && _snapshot.Length == 0) {
                _target?.HandleEmptySearchEscape();
            } else {
                _field.Text = _snapshot;
                _target?.FocusList();
            }
        };

        // Up/Down both move to the list directly, matching the box's on-screen position
        // immediately above it - "an arrow key" always exits to the list per
        // openspec/specs/drillable-list/spec.md's Shared Quick-Search Wiring, never past it (e.g.
        // straight to the tab header). Left/Right are deliberately left alone - those are the
        // TextField's own in-field cursor-movement keys. Tab/Shift+Tab do the same conceptually,
        // but Terminal.Gui resolves those through ManagementTabs (the nearest enclosing TabGroup),
        // never offering them to an ordinary TabStop view's own KeyBindings first - see
        // ManagementTabs.AdvanceWithinPage, which is where that pairing is actually implemented.
        // Up/Down have no such special-casing, so they're handled directly here.
        AddCommand(Command.Up, () => { _target?.FocusList(); return true; });
        AddCommand(Command.Down, () => { _target?.FocusList(); return true; });
        KeyBindings.Add(Key.CursorUp, Command.Up);
        KeyBindings.Add(Key.CursorDown, Command.Down);
    }

    // Called by DrillableListView<T>.AttachFilterBox, not directly - links this box to the list
    // it filters.
    public void AttachTo(IFilterable target) => _target = target;

    // The only entry point that focuses this field - sets CanFocus true first (required by
    // Terminal.Gui's focus model before SetFocus can land here), snapshots the field's current
    // text (possibly non-empty, left over from a prior activation) for Esc to revert to, then
    // focuses it. Called only from "/" on the attached list.
    public void Activate()
    {
        CanFocus = true;
        _snapshot = _field.Text;
        _field.SetFocus();
    }

    // Generic focus-lost hook: whichever way focus leaves this field - Enter/Esc/Tab/Shift+Tab/
    // arrow calling IFilterable.FocusList(), or a mouse click elsewhere handled entirely by the
    // framework's own focus machinery - this always fires and is the single place CanFocus flips
    // back to false, closing the field off again until the next Activate(). See design.md's "One
    // generic focus-lost hook closes every exit path" decision.
    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        if (!newHasFocus) CanFocus = false;
    }

    // This box's attached list, or null if none - used by ManagementTabs.AdvanceWithinPage the
    // same way IFilterable.AttachedFilterBox is.
    public IFilterable? Target => _target;

    // Clears the field's text without notifying the attached target - for a caller (ReplaceItems)
    // that's about to re-derive its filtered view from scratch anyway and doesn't want a
    // redundant/reentrant ApplyFilter call ahead of its own authoritative one.
    public void ResetSilently()
    {
        if (_field.Text.Length == 0) return;
        _suppressChange = true;
        _field.Text = string.Empty;
        _suppressChange = false;
    }
}
