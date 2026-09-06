using lazynats.Components;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.ObjStore;

// Multi-field modal for creating an OBJ bucket - mirrors KVStore/CreateBucketDialog.cs's shape
// exactly (see openspec/changes/add-obj-bucket-create/design.md's "Dialog shape" decision), minus
// the Storage dropdown and History/Limit Marker TTL fields it has no analog for: Tab moves between
// fields, Enter-on-a-field is inert rather than "submit", Create is a single explicit button,
// Cancel is a plain mnemonic-less button for mouse users (Esc already cancels via Dialog<T>'s own
// built-in behavior). `initial` seeds every field, used to reopen the dialog pre-filled after a
// failed CreateObjectStoreAsync (see ObjTab).
internal sealed class CreateBucketDialog: Dialog<NewBucketOptions>
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _nameField;
    private readonly TextField _maxAgeField;
    private readonly Button _createButton;

    public CreateBucketDialog(NewBucketOptions? initial = null, bool isEdit = false)
    {
        Title = DialogText.Pad(isEdit ? "Edit Bucket" : "New Bucket");
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var nameLabel = new Label { Text = "Name", X = 0, Y = 0 };
        _nameField = new TextField {
            Text = initial?.Name ?? string.Empty, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        _nameField.ValueChanged += (_, _) => UpdateValidity();
        var nameFrame = WrapField(_nameField, 1);

        var maxAgeLabel = new Label { Text = "Max Age", X = 0, Y = 4 };
        _maxAgeField = new TextField { Text = initial?.MaxAge?.ToString() ?? string.Empty };
        _maxAgeField.ValueChanged += (_, _) => UpdateValidity();
        var maxAgeFrame = WrapField(_maxAgeField, 5);

        Add(nameLabel, nameFrame, maxAgeLabel, maxAgeFrame);

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

        // See Streams/CreateStreamDialog's identical block for why: in edit mode Name has
        // CanFocus=false, and nothing would otherwise be focused until the user's first Tab.
        if (isEdit) _maxAgeField.SetFocus();
    }

    // Enter pressed on a plain field (Name/Max Age) isn't consumed by that field itself, so it
    // bubbles up as an unhandled Accept instead of routing through Create's own Accepting event
    // above. See KVStore/CreateBucketDialog's identical override for the full rationale - left
    // un-overridden, Dialog<T>'s default handling would silently discard whatever was typed, as
    // though the user had pressed Esc.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static EditFrame WrapField(View field, int y)
    {
        // Same "TextField only paints under its own text" reasoning as PatternDialog/HeaderDialog
        // - EditFrame's own fill covers the rest of the field regardless of content.
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = 43, Height = 3,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
            // See Streams/CreateStreamDialog's identical WrapField for why: EditFrame's own
            // CanFocus is hardcoded true unconditionally, so without this a locked field's frame
            // becomes a "phantom" tab stop - Tab lands on it, drill-down into the unfocusable
            // field fails, and the frame is left holding focus with no interactive content.
            CanFocus = field.CanFocus, TabStop = field.TabStop,
        };
    }

    private void Commit()
    {
        if (!_createButton.Enabled) return;
        if (!NewBucketOptions.TryParseMaxAge(_maxAgeField.Text, out var maxAge)) return;

        Result = new NewBucketOptions(_nameField.Text.Trim(), maxAge);
        RequestStop();
    }

    private void UpdateValidity()
    {
        var nameValid = _nameField.Text.Trim().Length > 0;
        var maxAgeValid = NewBucketOptions.TryParseMaxAge(_maxAgeField.Text, out _);

        SetFieldValidity(_nameField, nameValid);
        SetFieldValidity(_maxAgeField, maxAgeValid);

        _createButton.Enabled = nameValid && maxAgeValid;
    }

    private static void SetFieldValidity(TextField field, bool valid) =>
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as
        // PatternDialog/HeaderDialog/KVStore/CreateBucketDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
