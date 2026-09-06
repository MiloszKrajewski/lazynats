using System.Collections.ObjectModel;
using lazynats.Components;
using lazynats.Publish;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Templates;

// Create/Edit modal - structurally PublishDialog (Subject/Headers/Payload) plus a Name field
// (locked once editing an existing template, per CreateKeyDialog's Name-on-edit convention) and a
// Payload Type selector between Headers and Payload, per design.md's "Create/Edit dialog"
// decision. Reuses Publish's HeaderEditorView/HeaderPair directly (internal, same assembly) rather
// than duplicating the header-list editing mechanics.
internal sealed class TemplateDialog: Dialog<Template>
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    // Same width PublishDialog uses throughout for Subject/Headers/Payload - Name and Payload Type
    // stay the same width too, so every field's frame lines up on the left and right edges.
    private const int FieldWidth = 76;

    private readonly TextField _nameField;
    private readonly TextField _subjectField;
    private readonly ObservableCollection<HeaderPair> _headers;
    private readonly DropDownList<PayloadType> _payloadTypeDropDown;
#pragma warning disable CS0618 // TextView is obsolete in favor of Terminal.Gui.Editor - see PublishDialog's identical suppression.
    private readonly TextView _payloadView;
#pragma warning restore CS0618
    private readonly Button _commitButton;

    public TemplateDialog(Template? initial = null, bool isEdit = false)
    {
        Title = isEdit ? " Edit Template " : " New Template ";
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var nameLabel = new Label { Text = "Name", X = 0, Y = 0 };
        _nameField = new TextField {
            Text = initial?.Name ?? string.Empty, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        _nameField.ValueChanged += (_, _) => UpdateValidity();
        var nameFrame = WrapField(_nameField, 1, 3);

        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 4 };
        _subjectField = new TextField { Text = initial?.Subject ?? string.Empty };
        _subjectField.ValueChanged += (_, _) => UpdateValidity();
        var subjectFrame = WrapField(_subjectField, 5, 3);

        var headersLabel = new Label { Text = "Headers", X = 0, Y = 8 };
        _headers = initial is null
            ? []
            : new ObservableCollection<HeaderPair>(initial.Headers.Select(pair => new HeaderPair(pair.Key, pair.Value)));
        var subjectBackground = _subjectField.GetAttributeForRole(VisualRole.Editable).Background;
        // HeaderEditorView never offers filter/search (see its own comment) - starts right where
        // the label ends, with at least 3 lines of content (frame height 8, minus EditFrame's
        // fixed 2-row top/bottom border).
        var headerEditor = new HeaderEditorView(_headers) { Background = subjectBackground };
        var headerFrame = WrapField(headerEditor, 9, 8);

        var payloadTypeLabel = new Label { Text = "Payload Type", X = 0, Y = 17 };
        _payloadTypeDropDown = new DropDownList<PayloadType> { Value = initial?.PayloadType ?? PayloadType.Text };
        Theme.ApplyEditableScheme(_payloadTypeDropDown);
        _payloadTypeDropDown.ValueChanged += (_, _) => UpdateValidity();
        var payloadTypeFrame = WrapField(_payloadTypeDropDown, 18, 3);

        var payloadLabel = new Label { Text = "Payload", X = 0, Y = 21 };
#pragma warning disable CS0618
        _payloadView = new TextView { Text = initial?.Payload ?? string.Empty, TabKeyAddsTab = false };
#pragma warning restore CS0618
        _payloadView.ContentsChanged += (_, _) => UpdateValidity();
        var payloadFrame = WrapField(_payloadView, 22, 11);

        Add(
            nameLabel, nameFrame, subjectLabel, subjectFrame, headersLabel, headerFrame,
            payloadTypeLabel, payloadTypeFrame, payloadLabel, payloadFrame);

        // Result is left unset, matching Esc's own cancellation convention - added before
        // Create/Save so it, not Cancel, stays the last-added, Enter-activated default button.
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(cancelButton);

        // Marks the event Handled - same reason CreateKeyDialog/PublishDialog's commit buttons do:
        // left unhandled, Dialog<T>'s own default "unhandled Accept -> RequestStop" behavior fires
        // regardless, closing the dialog even when Create/Save was pressed while a field was still
        // invalid.
        _commitButton = new Button { Text = isEdit ? "_Save" : "_Create" };
        _commitButton.Accepting += (_, e) => { e.Handled = true; Commit(); };
        AddButton(_commitButton);

        UpdateValidity();

        // See CreateKeyDialog's identical block for why: in edit mode Name has CanFocus=false, and
        // nothing would otherwise be focused until the user's first Tab.
        if (isEdit) _subjectField.SetFocus();
    }

    // Enter pressed on the Name/Subject field (or the Headers list, when it doesn't itself consume
    // Enter, or the Payload Type dropdown while closed) isn't consumed by it, so it bubbles up as
    // an unhandled Accept instead of routing through Create/Save's own Accepting event above - see
    // CreateKeyDialog/PublishDialog's identical override for the full rationale. Enter inside
    // Payload is consumed by TextView itself (inserts a newline) and never reaches here.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static EditFrame WrapField(View field, int y, int height)
    {
        // Same "TextField/TextView/DropDownList only paint under their own content" reasoning as
        // CreateKeyDialog/PublishDialog - EditFrame's own fill covers the rest of the field
        // regardless of content.
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = FieldWidth, Height = height,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
            // See CreateKeyDialog's identical WrapField for why: EditFrame's own CanFocus is
            // hardcoded true unconditionally, so without this a locked field's frame becomes a
            // "phantom" tab stop.
            CanFocus = field.CanFocus, TabStop = field.TabStop,
        };
    }

    private void Commit()
    {
        if (!_commitButton.Enabled) return;

        // Last-one-wins on a duplicate key rather than ToDictionary's throw-on-duplicate - the
        // stored document is a flat JSON object (one value per key), so a duplicate entered via
        // HeaderEditorView (which doesn't itself reject duplicates) has to collapse to one value
        // somehow rather than fail the whole commit.
        var headers = new Dictionary<string, string>();
        foreach (var pair in _headers) headers[pair.Key] = pair.Value;

        Result = new Template(
            _nameField.Text.Trim(), _subjectField.Text.Trim(), headers,
            _payloadTypeDropDown.Value ?? PayloadType.Text, _payloadView.Text);
        RequestStop();
    }

    private void UpdateValidity()
    {
        var nameValid = _nameField.Text.Trim().Length > 0;
        var subjectValid = _subjectField.Text.Trim().Length > 0;
        var payloadValid = PayloadValidation.IsValid(_payloadTypeDropDown.Value ?? PayloadType.Text, _payloadView.Text);

        SetFieldValidity(_nameField, nameValid);
        SetFieldValidity(_subjectField, subjectValid);
        SetFieldValidity(_payloadView, payloadValid);

        _commitButton.Enabled = nameValid && subjectValid && payloadValid;
    }

    private static void SetFieldValidity(View field, bool valid) =>
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as
        // CreateKeyDialog/PublishDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
