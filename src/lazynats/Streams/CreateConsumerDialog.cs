using lazynats.Components;
using NATS.Client.JetStream.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Streams;

// DropDownList<TEnum> (confirmed by reflecting on the installed Terminal.Gui 2.4.10 package - no
// curated-subset constructor, and Source is inherited untyped from the non-generic base) only
// supports populating from every member of its type parameter - there's no way to hand it a
// subset of ConsumerConfigAckPolicy/ConsumerConfigDeliverPolicy directly. These two enums exist
// solely to give each dropdown its own narrower type to be exhaustive over; ToWire()/ToCurated()
// below convert at the dialog boundary, so nothing outside this file ever sees them.
//
// Excludes FlowControl: per its own doc comment, that's for durable consumers driving mirror/
// source replication, not a policy a user creating an ordinary consumer through this dialog
// should be choosing.
internal enum ConsumerCreateAckPolicy
{
    Explicit,
    All,
    None,
}

// Excludes ByStartSequence/ByStartTime: both need a companion OptStartSeq/OptStartTime value
// this dialog has no field for (Advanced fields, out of scope - see design.md's Non-Goals).
// Offering either here would let a user pick a Deliver Policy that's guaranteed to fail
// server-side with no client-side field to fix it before submitting.
internal enum ConsumerCreateDeliverPolicy
{
    All,
    Last,
    New,
    LastPerSubject,
}

// Multi-field modal for creating a consumer on the currently drilled-into stream (not itself a
// dialog field - see StreamsTab.OpenCreateConsumerDialog). Same shape as CreateStreamDialog: Tab
// moves between fields, Enter-on-a-field is a swallowed no-op, Create is the primary button, Esc
// cancels via Dialog<T>'s own built-in behavior, and Cancel is a plain mnemonic-less button for
// mouse users (see CreateStreamDialog for why it has no "C" hotkey). `initial` seeds every field,
// used to reopen the dialog pre-filled after a failed CreateConsumerAsync (see StreamsTab).
internal sealed class CreateConsumerDialog: Dialog<NewConsumerOptions>
{
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly TextField _nameField;
    private readonly TextField _filterSubjectsField;
    private readonly DropDownList<ConsumerCreateAckPolicy> _ackPolicyDropDown;
    private readonly DropDownList<ConsumerCreateDeliverPolicy> _deliverPolicyDropDown;
    private readonly Button _createButton;

    public CreateConsumerDialog(NewConsumerOptions? initial = null)
    {
        Title = DialogText.Pad("New Consumer");
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var nameLabel = new Label { Text = "Name", X = 0, Y = 0 };
        _nameField = new TextField { Text = initial?.Name ?? string.Empty };
        _nameField.ValueChanged += (_, _) => UpdateValidity();
        var nameFrame = WrapField(_nameField, 1);

        var filterSubjectsLabel = new Label { Text = "Filter Subjects", X = 0, Y = 4 };
        _filterSubjectsField = new TextField {
            Text = initial is null ? string.Empty : string.Join(", ", initial.FilterSubjects),
        };
        var filterSubjectsFrame = WrapField(_filterSubjectsField, 5);

        var ackPolicyLabel = new Label { Text = "Ack Policy", X = 0, Y = 8 };
        _ackPolicyDropDown = new DropDownList<ConsumerCreateAckPolicy> {
            Value = initial is null ? ConsumerCreateAckPolicy.Explicit : ToCurated(initial.AckPolicy),
        };
        Theme.ApplyEditableScheme(_ackPolicyDropDown);
        var ackPolicyFrame = WrapField(_ackPolicyDropDown, 9);

        var deliverPolicyLabel = new Label { Text = "Deliver Policy", X = 0, Y = 12 };
        _deliverPolicyDropDown = new DropDownList<ConsumerCreateDeliverPolicy> {
            Value = initial is null ? ConsumerCreateDeliverPolicy.All : ToCurated(initial.DeliverPolicy),
        };
        Theme.ApplyEditableScheme(_deliverPolicyDropDown);
        var deliverPolicyFrame = WrapField(_deliverPolicyDropDown, 13);

        Add(
            nameLabel, nameFrame, filterSubjectsLabel, filterSubjectsFrame,
            ackPolicyLabel, ackPolicyFrame, deliverPolicyLabel, deliverPolicyFrame);

        // Result is left unset (null), matching Esc's own cancellation convention. Added before
        // Create so Create - not Cancel - stays the last-added, Enter-activated default button.
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(cancelButton);

        // Marks the event Handled - same reason CreateStreamDialog's own Create button does: left
        // unhandled, Dialog<T>'s own default "unhandled Accept -> RequestStop" behavior fires
        // regardless of what Commit() decided, closing the dialog even when Create was
        // pressed/activated while Name was still invalid.
        _createButton = new Button { Text = "_Create" };
        _createButton.Accepting += (_, e) => { e.Handled = true; Commit(); };
        AddButton(_createButton);

        UpdateValidity();
    }

    // Same reasoning as CreateStreamDialog.OnAccepting: Enter on a plain field bubbles up as an
    // unhandled Accept rather than being consumed by the field itself, and Dialog<T>'s default
    // handling of that is an unconditional RequestStop() with Result unset - silently discarding
    // whatever was typed as though Esc had been pressed. Swallowing it here keeps Tab as the only
    // way to move between fields; Create itself still works via its own Accepting handler above.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static EditFrame WrapField(View field, int y)
    {
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = 43, Height = 3,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
        };
    }

    private void Commit()
    {
        if (!_createButton.Enabled) return;

        Result = new NewConsumerOptions(
            _nameField.Text.Trim(),
            NewStreamOptions.ParseSubjects(_filterSubjectsField.Text),
            ToWire(_ackPolicyDropDown.Value ?? ConsumerCreateAckPolicy.Explicit),
            ToWire(_deliverPolicyDropDown.Value ?? ConsumerCreateDeliverPolicy.All));
        RequestStop();
    }

    private static ConsumerCreateAckPolicy ToCurated(ConsumerConfigAckPolicy policy) => policy switch {
        ConsumerConfigAckPolicy.Explicit => ConsumerCreateAckPolicy.Explicit,
        ConsumerConfigAckPolicy.All => ConsumerCreateAckPolicy.All,
        ConsumerConfigAckPolicy.None => ConsumerCreateAckPolicy.None,
        // FlowControl can only reach here via `initial` (a retry-seed), and this dialog never
        // writes FlowControl into a NewConsumerOptions it produces - unreachable in practice.
        _ => ConsumerCreateAckPolicy.Explicit,
    };

    private static ConsumerConfigAckPolicy ToWire(ConsumerCreateAckPolicy policy) => policy switch {
        ConsumerCreateAckPolicy.Explicit => ConsumerConfigAckPolicy.Explicit,
        ConsumerCreateAckPolicy.All => ConsumerConfigAckPolicy.All,
        ConsumerCreateAckPolicy.None => ConsumerConfigAckPolicy.None,
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };

    private static ConsumerCreateDeliverPolicy ToCurated(ConsumerConfigDeliverPolicy policy) => policy switch {
        ConsumerConfigDeliverPolicy.All => ConsumerCreateDeliverPolicy.All,
        ConsumerConfigDeliverPolicy.Last => ConsumerCreateDeliverPolicy.Last,
        ConsumerConfigDeliverPolicy.New => ConsumerCreateDeliverPolicy.New,
        ConsumerConfigDeliverPolicy.LastPerSubject => ConsumerCreateDeliverPolicy.LastPerSubject,
        // ByStartSequence/ByStartTime can only reach here via `initial` (a retry-seed), and this
        // dialog never writes either into a NewConsumerOptions it produces - unreachable in
        // practice.
        _ => ConsumerCreateDeliverPolicy.All,
    };

    private static ConsumerConfigDeliverPolicy ToWire(ConsumerCreateDeliverPolicy policy) => policy switch {
        ConsumerCreateDeliverPolicy.All => ConsumerConfigDeliverPolicy.All,
        ConsumerCreateDeliverPolicy.Last => ConsumerConfigDeliverPolicy.Last,
        ConsumerCreateDeliverPolicy.New => ConsumerConfigDeliverPolicy.New,
        ConsumerCreateDeliverPolicy.LastPerSubject => ConsumerConfigDeliverPolicy.LastPerSubject,
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };

    // Filter Subjects and both dropdowns are always valid (an empty Filter Subjects parse means
    // "no filter," not an error - unlike NewStreamOptions.Subjects) - only Name can block Create.
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
        // CreateStreamDialog).
        field.SetScheme(valid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
}
