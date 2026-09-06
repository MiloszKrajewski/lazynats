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

    public FilterBox()
    {
        CanFocus = true;
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

        // Esc: clear-then-return-focus-to-the-list when there's text to clear; otherwise defer to
        // the attached list's own idea of what an empty-field Esc should do - see
        // IFilterable.HandleEmptySearchEscape. Esc reaches ordinary key-preprocessing (KeyDown), so
        // it's handled that way, unlike Tab/Shift+Tab/Down below.
        _field.KeyDown += (_, key) => {
            if (key != Key.Esc) return;
            key.Handled = true;
            if (_field.Text.Length > 0) {
                _field.Text = string.Empty;
                _target?.FocusList();
            } else {
                _target?.HandleEmptySearchEscape();
            }
        };

        // Down moves to the list directly, matching the box's on-screen position immediately above
        // it. Tab/Shift+Tab do the same conceptually, but Terminal.Gui resolves those through
        // ManagementTabs (the nearest enclosing TabGroup), never offering them to an ordinary
        // TabStop view's own KeyBindings first - see ManagementTabs.AdvanceWithinPage, which is
        // where that pairing is actually implemented. Down has no such special-casing, so it's
        // handled directly here.
        AddCommand(Command.Down, () => { _target?.FocusList(); return true; });
        KeyBindings.Add(Key.CursorDown, Command.Down);
    }

    // Called by DrillableListView<T>.AttachFilterBox, not directly - links this box to the list
    // it filters.
    public void AttachTo(IFilterable target) => _target = target;

    public void Focus() => _field.SetFocus();

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
