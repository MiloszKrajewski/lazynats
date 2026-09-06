using System.Collections.ObjectModel;
using lazynats.Components;
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
    private static readonly Attribute InvalidSubject = new(ColorName16.Red, Theme.EditableBackground);

    private readonly NatsConnection _connection;
    private readonly ObservableCollection<HeaderPair> _headers = [];
    private readonly TextField _subjectField;
#pragma warning disable CS0618 // TextView is obsolete in favor of Terminal.Gui.Editor - see PublishTab's identical (now removed) suppression.
    private readonly TextView _payloadView;
#pragma warning restore CS0618
    private readonly Label _statusLabel;
    private readonly Button _sendButton;

    public PublishDialog(NatsConnection connection)
    {
        _connection = connection;
        Title = DialogText.Pad("Publish");
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        _subjectField = new TextField();
        _subjectField.ValueChanged += (_, _) => UpdateValidity();
        var subjectFrame = WrapField(_subjectField, 1, 3);

        var headersLabel = new Label { Text = "Headers", X = 0, Y = 4 };
        var subjectBackground = _subjectField.GetAttributeForRole(VisualRole.Editable).Background;
        // FilterBox's own fixed Height (3) pushes headerFrame and everything below it down by 3
        // rows relative to before this field existed - see the hand-adjusted Y values below.
        var headerFilterBox = new FilterBox { X = 0, Y = 5, Width = FieldWidth };
        var headerEditor = new HeaderEditorView(_headers) { Background = subjectBackground };
        headerEditor.AttachFilterBox(headerFilterBox);
        // Height 5 -> 3 visible rows inside EditFrame's 1-row top/bottom border, enough to show
        // a few headers at once without scrolling immediately.
        var headerFrame = WrapField(headerEditor, 8, 5);

        var payloadLabel = new Label { Text = "Payload", X = 0, Y = 13 };
        // TabKeyAddsTab = false stops TextView from consuming Tab at all (mirroring
        // CreateKeyDialog's Value field), so Tab reaches normal focus-advance handling with no
        // Navigate/Edit mode needed - see design.md's "Payload uses TabKeyAddsTab = false" decision.
#pragma warning disable CS0618
        _payloadView = new TextView { TabKeyAddsTab = false };
#pragma warning restore CS0618
        // Height 11 -> 9 visible rows, room for a multi-line JSON/text payload.
        var payloadFrame = WrapField(_payloadView, 14, 11);

        _statusLabel = new Label { Text = string.Empty, X = 0, Y = 25 };

        // Add()-order matches spatial top-down layout (label, then FilterBox, then its list) so
        // Tab/Shift+Tab cycles in reading order - mirrors StreamsTab/ValuesTab/ObjectsTab/
        // SubscribeTab.
        Add(
            subjectLabel, subjectFrame, headersLabel, headerFilterBox, headerFrame, payloadLabel, payloadFrame,
            _statusLabel);

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
        var valid = _subjectField.Text.Trim().Length > 0;
        _sendButton.Enabled = valid;
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (EditFrame's own
        // background is never touched here either, for the same reason).
        _subjectField.SetScheme(valid ? null : new Scheme(InvalidSubject) { Editable = InvalidSubject });
    }

    private void Send()
    {
        var subject = _subjectField.Text.Trim();
        if (subject.Length == 0) return;

        NatsHeaders? headers = null;
        if (_headers.Count > 0) {
            headers = new NatsHeaders();
            foreach (var pair in _headers) headers.Add(pair.Key, pair.Value);
        }

        _ = PublishAsync(subject, headers, _payloadView.Text);
    }

    private async Task PublishAsync(string subject, NatsHeaders? headers, string payload)
    {
        try {
            await _connection.PublishAsync(subject, payload, headers: headers);
            App?.Invoke(RequestStop);
        } catch (Exception ex) {
            App?.Invoke(() => _statusLabel.Text = $"Publish failed: {ex.Message}");
        }
    }
}
