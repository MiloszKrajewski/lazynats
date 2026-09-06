using lazynats.Components;
using NATS.Client.KeyValueStore;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Values;

// Multi-field modal for creating a KV bucket - mirrors Streams/CreateStreamDialog.cs's shape
// exactly (see openspec/changes/add-kv-bucket-create/design.md's "Dialog shape" decision): Tab
// moves between fields, Enter-on-a-field is inert rather than "submit", Create is a single
// explicit button, Cancel is a plain mnemonic-less button for mouse users (Esc already cancels
// via Dialog<T>'s own built-in behavior). `initial` seeds every field, used to reopen the dialog
// pre-filled after a failed CreateStoreAsync (see ValuesTab).
internal sealed class CreateBucketDialog: Dialog<NewBucketOptions>
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _nameField;
    private readonly DropDownList<NatsKVStorageType> _storageDropDown;
    private readonly TextField _historyField;
    private readonly TextField _maxAgeField;
    private readonly TextField _limitMarkerTtlField;
    private readonly Button _createButton;

    public CreateBucketDialog(NewBucketOptions? initial = null, bool isEdit = false)
    {
        Title = isEdit ? " Edit Bucket " : " New Bucket ";
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var nameLabel = new Label { Text = "Name", X = 0, Y = 0 };
        _nameField = new TextField {
            Text = initial?.Name ?? string.Empty, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        _nameField.ValueChanged += (_, _) => UpdateValidity();
        _nameField.FixPasteRedraw();
        var nameFrame = WrapField(_nameField, 1);

        var storageLabel = new Label { Text = "Storage", X = 0, Y = 4 };
        _storageDropDown = new DropDownList<NatsKVStorageType> {
            Value = initial?.Storage ?? NatsKVStorageType.File, Enabled = !isEdit, CanFocus = !isEdit,
            TabStop = isEdit ? TabBehavior.NoStop : TabBehavior.TabStop,
        };
        Theme.ApplyEditableScheme(_storageDropDown);
        var storageFrame = WrapField(_storageDropDown, 5);

        var historyLabel = new Label { Text = "History", X = 0, Y = 8 };
        _historyField = new TextField { Text = initial?.History?.ToString() ?? string.Empty };
        _historyField.ValueChanged += (_, _) => UpdateValidity();
        _historyField.FixPasteRedraw();
        var historyFrame = WrapField(_historyField, 9);

        var maxAgeLabel = new Label { Text = "Max Age", X = 0, Y = 12 };
        _maxAgeField = new TextField { Text = initial?.MaxAge?.ToString() ?? string.Empty };
        _maxAgeField.ValueChanged += (_, _) => UpdateValidity();
        _maxAgeField.FixPasteRedraw();
        var maxAgeFrame = WrapField(_maxAgeField, 13);

        var limitMarkerTtlLabel = new Label { Text = "Limit Marker TTL", X = 0, Y = 16 };
        _limitMarkerTtlField = new TextField { Text = initial?.LimitMarkerTTL?.ToString() ?? string.Empty };
        _limitMarkerTtlField.ValueChanged += (_, _) => UpdateValidity();
        _limitMarkerTtlField.FixPasteRedraw();
        var limitMarkerTtlFrame = WrapField(_limitMarkerTtlField, 17);

        Add(
            nameLabel, nameFrame, storageLabel, storageFrame,
            historyLabel, historyFrame, maxAgeLabel, maxAgeFrame,
            limitMarkerTtlLabel, limitMarkerTtlFrame);

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

        // See Streams/CreateStreamDialog's identical block for why: in edit mode Name and Storage
        // have CanFocus=false, and nothing would otherwise be focused until the user's first Tab.
        if (isEdit) _historyField.SetFocus();
    }

    // Enter pressed on a plain field (Name/History/Max Age/Limit Marker TTL, or the Storage
    // dropdown while closed) isn't consumed by that field itself, so it bubbles up as an
    // unhandled Accept instead of routing through Create's own Accepting event above. See
    // CreateStreamDialog's identical override for the full rationale - left un-overridden,
    // Dialog<T>'s default handling would silently discard whatever was typed, as though the user
    // had pressed Esc.
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
        if (!NewBucketOptions.TryParseHistory(_historyField.Text, out var history)) return;
        if (!NewBucketOptions.TryParseMaxAge(_maxAgeField.Text, out var maxAge)) return;
        if (!NewBucketOptions.TryParseLimitMarkerTTL(_limitMarkerTtlField.Text, out var limitMarkerTtl)) return;

        Result = new NewBucketOptions(
            _nameField.Text.Trim(),
            _storageDropDown.Value ?? NatsKVStorageType.File,
            history,
            maxAge,
            limitMarkerTtl);
        RequestStop();
    }

    private void UpdateValidity()
    {
        var nameValid = _nameField.Text.Trim().Length > 0;
        var historyValid = NewBucketOptions.TryParseHistory(_historyField.Text, out _);
        var maxAgeValid = NewBucketOptions.TryParseMaxAge(_maxAgeField.Text, out _);
        var limitMarkerTtlValid = NewBucketOptions.TryParseLimitMarkerTTL(_limitMarkerTtlField.Text, out _);

        SetFieldValidity(_nameField, nameValid);
        SetFieldValidity(_historyField, historyValid);
        SetFieldValidity(_maxAgeField, maxAgeValid);
        SetFieldValidity(_limitMarkerTtlField, limitMarkerTtlValid);

        _createButton.Enabled = nameValid && historyValid && maxAgeValid && limitMarkerTtlValid;
    }

    private static void SetFieldValidity(TextField field, bool valid) =>
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as
        // PatternDialog/HeaderDialog/CreateStreamDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
