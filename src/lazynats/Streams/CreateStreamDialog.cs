using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Streams;

// Multi-field modal for creating a stream - the first Dialog<T> in the codebase with more than
// one field (PatternDialog/HeaderDialog are both single-TextField precedent that commit on
// Enter). Tab has to be free to move between fields here, so Enter-on-a-field is deliberately
// inert rather than "submit" - Create is a single explicit button instead (clicked, or reached
// via Tab and activated with Enter/Space). Cancel is a plain, mnemonic-less button ("Cancel", not
// "_Cancel") purely for mouse users - Esc already cancels via Dialog<T>'s own built-in behavior,
// and a keyboard mnemonic sharing "C" with "_Create" would only collide. `initial` seeds every
// field, used to reopen the dialog pre-filled after a failed CreateStreamAsync (see StreamsTab).
internal sealed class CreateStreamDialog: Dialog<NewStreamOptions>
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _nameField;
    private readonly TextField _subjectsField;
    private readonly DropDownList<StreamConfigRetention> _retentionDropDown;
    private readonly TextField _maxAgeField;
    private readonly Button _createButton;

    public CreateStreamDialog(NewStreamOptions? initial = null, bool isEdit = false)
    {
        Title = isEdit ? " Edit Stream " : " New Stream ";
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var nameLabel = new Label { Text = "Name", X = 0, Y = 0 };
        _nameField = new TextField {
            Text = initial?.Name ?? string.Empty, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        _nameField.ValueChanged += (_, _) => UpdateValidity();
        _nameField.FixPasteRedraw();
        var nameFrame = WrapField(_nameField, 1);

        var subjectsLabel = new Label { Text = "Subjects", X = 0, Y = 4 };
        _subjectsField = new TextField { Text = initial is null ? string.Empty : string.Join(", ", initial.Subjects) };
        _subjectsField.ValueChanged += (_, _) => UpdateValidity();
        _subjectsField.FixPasteRedraw();
        var subjectsFrame = WrapField(_subjectsField, 5);

        var retentionLabel = new Label { Text = "Retention", X = 0, Y = 8 };
        _retentionDropDown = new DropDownList<StreamConfigRetention> {
            Value = initial?.Retention ?? StreamConfigRetention.Limits, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        Theme.ApplyEditableScheme(_retentionDropDown);
        var retentionFrame = WrapField(_retentionDropDown, 9);

        var maxAgeLabel = new Label { Text = "Max Age", X = 0, Y = 12 };
        _maxAgeField = new TextField { Text = initial?.MaxAge?.ToString() ?? string.Empty };
        _maxAgeField.ValueChanged += (_, _) => UpdateValidity();
        _maxAgeField.FixPasteRedraw();
        var maxAgeFrame = WrapField(_maxAgeField, 13);

        Add(nameLabel, nameFrame, subjectsLabel, subjectsFrame, retentionLabel, retentionFrame, maxAgeLabel, maxAgeFrame);

        // Result is left unset (null), matching Esc's own cancellation convention. Added before
        // Create so Create - not Cancel - stays the last-added, Enter-activated default button.
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(cancelButton);

        // Marks the event Handled - same reason PatternDialog's field-level Accepting handler
        // does: left unhandled, Dialog<T>'s own default "unhandled Accept -> RequestStop"
        // behavior fires regardless of what Commit() decided, closing the dialog even when Create
        // was pressed/activated while a field was still invalid.
        _createButton = new Button { Text = isEdit ? "_Save" : "_Create" };
        _createButton.Accepting += (_, e) => { e.Handled = true; Commit(); };
        AddButton(_createButton);

        UpdateValidity();

        // In edit mode Name (the first Add()-ed field) has CanFocus=false, and Terminal.Gui's
        // open-time initial-focus assignment doesn't walk forward to the next focusable control -
        // left alone, nothing would be focused until the user's first Tab. Explicitly focusing the
        // first editable field skips that dead first keystroke.
        if (isEdit) _subjectsField.SetFocus();
    }

    // Enter pressed on a plain field (Name/Subjects/Max Age, or the Retention dropdown while
    // closed) isn't consumed by that field itself, so it bubbles up as an unhandled Accept
    // instead of routing through Create's own Accepting event above. Left un-overridden,
    // Dialog<T>'s own default handling of that bubbled, unowned Accept is an unconditional
    // RequestStop() with Result left unset (verified empirically), silently discarding whatever
    // was typed as though the user had pressed Esc. Swallowing it here (return true, no Commit())
    // instead makes Enter-on-a-field a true no-op - matching the class comment's "Tab must be
    // free to move between fields" intent. Reaching Create itself still works exactly as before,
    // via its own Accepting handler above, which runs before the event would ever bubble here.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static EditFrame WrapField(View field, int y)
    {
        // Same "TextField only paints under its own text" reasoning as PatternDialog/HeaderDialog
        // - EditFrame's own fill covers the rest of the field regardless of content.
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = 43, Height = 3,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
            // EditFrame's own CanFocus is hardcoded true unconditionally (it never holds focus
            // itself - Tab is meant to drill straight through to `field`, per its own class
            // comment). When `field` is a locked (CanFocus=false) field, that leaves the frame as
            // a "phantom" tab stop: Tab lands on the frame, its own drill-down into the
            // unfocusable field fails, and the frame itself is left holding focus with no
            // interactive content, wasting a Tab press. Propagating `field`'s CanFocus/TabStop
            // onto the frame keeps the two in lockstep, so a locked field's frame is excluded from
            // the dialog's tab order right along with it.
            CanFocus = field.CanFocus, TabStop = field.TabStop,
        };
    }

    private void Commit()
    {
        if (!_createButton.Enabled) return;
        if (!NewStreamOptions.TryParseMaxAge(_maxAgeField.Text, out var maxAge)) return;

        Result = new NewStreamOptions(
            _nameField.Text.Trim(),
            NewStreamOptions.ParseSubjects(_subjectsField.Text),
            _retentionDropDown.Value ?? StreamConfigRetention.Limits,
            maxAge);
        RequestStop();
    }

    private void UpdateValidity()
    {
        var nameValid = _nameField.Text.Trim().Length > 0;
        var subjectsValid = NewStreamOptions.ParseSubjects(_subjectsField.Text).Count > 0;
        var maxAgeValid = NewStreamOptions.TryParseMaxAge(_maxAgeField.Text, out _);

        SetFieldValidity(_nameField, nameValid);
        SetFieldValidity(_subjectsField, subjectsValid);
        SetFieldValidity(_maxAgeField, maxAgeValid);

        _createButton.Enabled = nameValid && subjectsValid && maxAgeValid;
    }

    private static void SetFieldValidity(TextField field, bool valid) =>
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as
        // PatternDialog/HeaderDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
