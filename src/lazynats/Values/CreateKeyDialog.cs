using lazynats.Components;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Values;

// Multi-field modal for creating/editing a KV key - mirrors CreateBucketDialog's shape (Tab
// between fields, Enter-on-a-field inert, Create/Save as a single explicit button, Cancel for
// mouse users), except Value is a TextView rather than a TextField: see
// openspec/changes/add-kv-key-crud/design.md's "Value field is a multi-line TextView" decision.
// `initial` seeds every field, used both to open in edit mode (seeded with the key's current
// decoded value - see ValuesTab.OpenEditKeyDialog) and to reopen pre-filled after a failed
// PutAsync (see ValuesTab).
internal sealed class CreateKeyDialog: Dialog<NewKeyOptions>
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _nameField;
#pragma warning disable CS0618 // TextView is obsolete in favor of Terminal.Gui.Editor - see PublishDialog's identical suppression.
    private readonly TextView _valueView;
#pragma warning restore CS0618
    private readonly Button _createButton;

    public CreateKeyDialog(NewKeyOptions? initial = null, bool isEdit = false)
    {
        Title = isEdit ? " Edit Key " : " New Key ";
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var nameLabel = new Label { Text = "Name", X = 0, Y = 0 };
        _nameField = new TextField {
            Text = initial?.Name ?? string.Empty, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        _nameField.ValueChanged += (_, _) => UpdateValidity();
        var nameFrame = WrapField(_nameField, 1, 3);

        var valueLabel = new Label { Text = "Value", X = 0, Y = 4 };
#pragma warning disable CS0618
        _valueView = new TextView { Text = initial?.Value ?? string.Empty, TabKeyAddsTab = false };
#pragma warning restore CS0618
        var valueFrame = WrapField(_valueView, 5, 10);

        Add(nameLabel, nameFrame, valueLabel, valueFrame);

        // Result is left unset (null), matching Esc's own cancellation convention. Added before
        // Create so Create - not Cancel - stays the last-added, Enter-activated default button.
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(cancelButton);

        // Marks the event Handled - same reason CreateBucketDialog's Create button does: left
        // unhandled, Dialog<T>'s own default "unhandled Accept -> RequestStop" behavior fires
        // regardless of what Commit() decided, closing the dialog even when Create was pressed
        // while the Name field was still invalid.
        _createButton = new Button { Text = isEdit ? "_Save" : "_Create" };
        _createButton.Accepting += (_, e) => { e.Handled = true; Commit(); };
        AddButton(_createButton);

        UpdateValidity();

        // See CreateBucketDialog's identical block for why: in edit mode Name has CanFocus=false,
        // and nothing would otherwise be focused until the user's first Tab.
        if (isEdit) _valueView.SetFocus();
    }

    // Enter pressed on the Name field isn't consumed by it, so it bubbles up as an unhandled
    // Accept instead of routing through Create's own Accepting event above - see
    // CreateBucketDialog's identical override for the full rationale. Enter inside Value is
    // consumed by TextView itself (inserts a newline, per EnterKeyAddsLine's default) and never
    // reaches here.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static EditFrame WrapField(View field, int y, int height)
    {
        // Same "TextField/TextView only paint under their own content" reasoning as
        // CreateBucketDialog/PublishDialog - EditFrame's own fill covers the rest of the field
        // regardless of content.
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = 43, Height = height,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
            // See CreateBucketDialog's identical WrapField for why: EditFrame's own CanFocus is
            // hardcoded true unconditionally, so without this a locked field's frame becomes a
            // "phantom" tab stop.
            CanFocus = field.CanFocus, TabStop = field.TabStop,
        };
    }

    private void Commit()
    {
        if (!_createButton.Enabled) return;

        Result = new NewKeyOptions(_nameField.Text.Trim(), _valueView.Text);
        RequestStop();
    }

    private void UpdateValidity()
    {
        var nameValid = _nameField.Text.Trim().Length > 0;
        SetFieldValidity(_nameField, nameValid);
        _createButton.Enabled = nameValid;
    }

    private static void SetFieldValidity(TextField field, bool valid) =>
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as
        // CreateBucketDialog/CreateStreamDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
