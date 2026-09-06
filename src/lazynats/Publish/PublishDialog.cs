using System.Collections.ObjectModel;
using lazynats.Components;
using lazynats.Payloads;
using NATS.Client.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Publish;

// Modal counterpart to the old PublishTab (see openspec/changes/publish-dialog/design.md):
// composing/sending one message is a one-off action, not a standing view, so it now lives behind
// Alt+P instead of its own permanent tab. Unlike every Dialog<T> elsewhere in this codebase, this
// is a plain Dialog - nothing outside the dialog needs the composed message as a value; the
// dialog publishes it directly via its injected NatsConnection, same as PublishTab did. A
// successful Send closes the dialog (RequestStop in PublishAsync's success branch); a failed one
// leaves it open with the error in the status label so the message can be fixed and resent.
internal sealed class PublishDialog: Dialog
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly NatsConnection _connection;
    private readonly ObservableCollection<HeaderPair> _headers = [];
    private readonly TextField _subjectField;
    private readonly DropDownList<PayloadType> _payloadTypeDropDown;
#pragma warning disable CS0618 // TextView is obsolete in favor of Terminal.Gui.Editor - see PublishTab's identical (now removed) suppression.
    private readonly TextView _payloadView;
#pragma warning restore CS0618
    private readonly Label _statusLabel;
    private readonly Button _sendButton;

    public PublishDialog(NatsConnection connection)
    {
        _connection = connection;
        Title = " Publish ";
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        _subjectField = new TextField();
        _subjectField.ValueChanged += (_, _) => UpdateValidity();
        _subjectField.FixPasteRedraw();
        var subjectFrame = WrapField(_subjectField, 1, 3);

        var headersLabel = new Label { Text = "Headers", X = 0, Y = 4 };
        var subjectBackground = _subjectField.GetAttributeForRole(VisualRole.Editable).Background;
        // No FilterBox - HeaderEditorView never offers filter/search (see its own comment); the
        // frame starts right where the label ends instead. Height 8 -> 6 visible rows inside
        // EditFrame's 1-row top/bottom border, enough to show several headers at once without
        // scrolling immediately.
        var headerEditor = new HeaderEditorView(_headers) { Background = subjectBackground };
        var headerFrame = WrapField(headerEditor, 5, 8);

        var payloadTypeLabel = new Label { Text = "Payload Type", X = 0, Y = 13 };
        _payloadTypeDropDown = new DropDownList<PayloadType> { Value = PayloadType.Text };
        Theme.ApplyEditableScheme(_payloadTypeDropDown);
        _payloadTypeDropDown.ValueChanged += (_, _) => UpdateValidity();
        var payloadTypeFrame = WrapField(_payloadTypeDropDown, 14, 3);

        var payloadLabel = new Label { Text = "Payload", X = 0, Y = 17 };
        // TabKeyAddsTab = false stops TextView from consuming Tab at all (mirroring
        // CreateKeyDialog's Value field), so Tab reaches normal focus-advance handling with no
        // Navigate/Edit mode needed - see design.md's "Payload uses TabKeyAddsTab = false" decision.
#pragma warning disable CS0618
        _payloadView = new TextView { TabKeyAddsTab = false };
        _payloadView.ContentsChanged += (_, _) => UpdateValidity();
        _payloadView.FixPasteRedraw();
#pragma warning restore CS0618
        // Height 11 -> 9 visible rows, room for a multi-line JSON/text payload.
        var payloadFrame = WrapField(_payloadView, 18, 11);

        _statusLabel = new Label { Text = string.Empty, X = 0, Y = 29 };

        Add(
            subjectLabel, subjectFrame, headersLabel, headerFrame, payloadTypeLabel, payloadTypeFrame,
            payloadLabel, payloadFrame, _statusLabel);

        // Result is left unset, matching Esc's own cancellation convention - added before Send so
        // Send, not Cancel, stays the last-added, Enter-activated default button.
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(cancelButton);

        // Marks the event Handled - same reason CreateBucketDialog/CreateKeyDialog's commit
        // buttons do: left unhandled, Dialog's own default "unhandled Accept -> RequestStop"
        // behavior fires regardless, closing the dialog even when Send was pressed while Subject
        // was still invalid.
        _sendButton = new Button { Text = "_Send" };
        _sendButton.Accepting += (_, e) => { e.Handled = true; Send(); };
        AddButton(_sendButton);

        UpdateValidity();
    }

    // Enter pressed on the Subject field (or the Headers list, when it doesn't itself consume
    // Enter) isn't consumed by it, so it bubbles up as an unhandled Accept instead of routing
    // through Send's own Accepting event above - see CreateBucketDialog/CreateKeyDialog's
    // identical override for the full rationale. Enter inside Payload is consumed by TextView
    // itself (inserts a newline) and never reaches here.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    // Wider than the 43-column width most other dialogs use (CreateBucketDialog, CreateKeyDialog,
    // ...): those hold short single-line values (names, numbers), while Publish's Subject/Headers/
    // Payload routinely carry long NATS subjects and JSON payloads that benefit from the extra room.
    private const int FieldWidth = 76;

    private static EditFrame WrapField(View field, int y, int height)
    {
        // Same "TextField/TextView only paint under their own content" reasoning as
        // CreateBucketDialog/CreateKeyDialog - EditFrame's own fill covers the rest of the field
        // regardless of content.
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = FieldWidth, Height = height,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
        };
    }

    private void UpdateValidity()
    {
        var subjectValid = _subjectField.Text.Trim().Length > 0;
        var payloadType = _payloadTypeDropDown.Value ?? PayloadType.Text;
        var payloadValid = PayloadValidation.IsValid(payloadType, _payloadView.Text);

        _sendButton.Enabled = subjectValid && payloadValid;
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (EditFrame's own
        // background is never touched here either, for the same reason).
        _subjectField.SetScheme(subjectValid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
        _payloadView.SetScheme(payloadValid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
    }

    private void Send()
    {
        var subject = _subjectField.Text.Trim();
        var payloadType = _payloadTypeDropDown.Value ?? PayloadType.Text;
        var payload = _payloadView.Text;
        if (subject.Length == 0 || !PayloadValidation.IsValid(payloadType, payload)) return;

        NatsHeaders? headers = null;
        if (_headers.Count > 0) {
            headers = new NatsHeaders();
            foreach (var pair in _headers) headers.Add(pair.Key, pair.Value);
        }

        _ = PublishAsync(subject, headers, payloadType, payload);
    }

    private async Task PublishAsync(string subject, NatsHeaders? headers, PayloadType payloadType, string payload)
    {
        try {
            var bytes = PayloadEncoding.ToBytes(payloadType, payload);
            await _connection.PublishAsync(subject, bytes, headers: headers);
            App?.Invoke(RequestStop);
        } catch (Exception ex) {
            App?.Invoke(() => _statusLabel.Text = $"Publish failed: {ex.Message}");
        }
    }
}
