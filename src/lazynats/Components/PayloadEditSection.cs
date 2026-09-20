using System.Text;
using lazynats.Core.Payloads;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Components;

// The editable counterpart to PayloadDetailSection: a Payload Type dropdown (fixed-height row)
// above a payload/value TextView (fills whatever Height the section is given), shared by
// PublishDialog/TemplateDialog/CreateKeyDialog instead of each hand-rolling the same
// dropdown/TextView/validity wiring independently. See
// openspec/changes/extract-payload-edit-section/design.md.
internal sealed class PayloadEditSection: View
{
    // Label row (1) + EditFrame border (2) = 3 - just enough for the dropdown's single content row.
    private const int TypeDropDownHeight = 3;

    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);

    private readonly DropDownList<PayloadType> _typeDropDown;
#pragma warning disable CS0618 // TextView is obsolete in favor of Terminal.Gui.Editor - see PublishDialog's identical suppression.
    private readonly TextView _payloadView;
#pragma warning restore CS0618

    public event Action? Changed;

    // `initialType`/`initialText` seed the field verbatim, no rendering - used for a blank Create
    // and for reopening pre-filled with exactly what the user last typed after a failed save (see
    // design.md's "Two seeding entry points, not one" decision). A genuine Edit seeded from an
    // existing payload's raw bytes goes through SeedFromBytes instead, called by the owner once its
    // own initial layout has run (mirrors PayloadDetailSection's construct-then-measure pattern).
    public PayloadEditSection(string label, PayloadType initialType, string initialText)
    {
        // Terminal.Gui requires CanFocus=true on every ancestor for a descendant to be focusable at
        // all - see EditFrame's own identical comment. Both the dropdown and the payload TextView
        // below are meant to be reachable via ordinary Tab navigation, so this section (their
        // shared ancestor) needs it too.
        CanFocus = true;

        var typeLabel = new Label { Text = "Payload Type", X = 0, Y = 0 };
        _typeDropDown = new DropDownList<PayloadType> { Value = initialType };
        Theme.ApplyEditableScheme(_typeDropDown);
        _typeDropDown.ValueChanged += (_, _) => UpdateValidity();
        var typeFrame = WrapField(_typeDropDown, 1, TypeDropDownHeight);

        var payloadLabel = new Label { Text = label, X = 0, Y = 4 };
#pragma warning disable CS0618
        // TabKeyAddsTab = false so Tab reaches normal focus-advance handling instead of being
        // consumed as a literal tab character - see PublishDialog's identical field for the full
        // rationale.
        _payloadView = new TextView { Text = initialText, TabKeyAddsTab = false };
#pragma warning restore CS0618
        _payloadView.ContentsChanged += (_, _) => UpdateValidity();
        _payloadView.FixPasteRedraw();
        var payloadFrame = WrapField(_payloadView, 5, Dim.Fill());

        Add(typeLabel, typeFrame, payloadLabel, payloadFrame);

        UpdateValidity();
    }

    // Never null in practice (DropDownList<PayloadType>.Value always holds a value once
    // constructed with one), but every dialog this replaces guarded the same read with an
    // `?? PayloadType.Text` fallback - mirrored here as the same safety net.
    public PayloadType Type => _typeDropDown.Value ?? PayloadType.Text;

    // Named PayloadText, not Text - View already declares a Text property of its own.
    public string PayloadText => _payloadView.Text;

    public bool IsValid { get; private set; }

    // Only meaningful when IsValid - callers gate on IsValid before reading this, same as every
    // dialog did with PayloadEncoding.ToBytes directly before this extraction.
    public byte[] Bytes => PayloadEncoding.ToBytes(Type, PayloadText);

    // Sends focus straight to the payload/value editor, bypassing the Payload Type dropdown -
    // used by an owning dialog's own isEdit-focus block (see CreateKeyDialog's identical original
    // `_valueView.SetFocus()`) where the field above this section (Name) is disabled and the next
    // tab stop would otherwise be this section's own Payload Type dropdown, not the editor.
    public void FocusPayload() => _payloadView.SetFocus();

    // Seeds from an existing payload's raw bytes, rendering per Payload Type: Json/Hex/Base64 via
    // PayloadPresentation.Render at this section's own resolved editor width, Text via a raw UTF-8
    // decode - PayloadPresentation's fixed-width chop is sized for a non-wrapping read-only
    // display and would double-wrap once combined with this editor's own word wrap (see
    // ValuesTab.SeedValueText's original comment, now unified here for every caller). Must be
    // called after the owner's initial Layout() has run, so this section's payload TextView
    // Viewport is resolved - mirrors PayloadDetailSection.MeasureAndRender's identical contract.
    public void SeedFromBytes(byte[] data, PayloadType type)
    {
        var width = _payloadView.Viewport.Width;
        var text = type == PayloadType.Text ? Encoding.UTF8.GetString(data) : PayloadPresentation.Render(data, type, width);
        _typeDropDown.Value = type;
        _payloadView.Text = text;
        UpdateValidity();
    }

    private void UpdateValidity()
    {
        var type = Type;
        // Json's own pretty-printed indentation has deliberate line structure that an added
        // soft-wrap would visually clash with - see PublishDialog's identical original comment.
        _payloadView.WordWrap = type != PayloadType.Json;
        IsValid = PayloadValidation.IsValid(type, _payloadView.Text);
        // new Scheme(Attribute)'s single-value constructor derives Editable independently and
        // silently drops our background (defaults it to Black) - re-set Editable explicitly so
        // invalid state only changes the foreground, never the background (same as every dialog
        // this replaces).
        _payloadView.SetScheme(IsValid ? null : new Scheme(InvalidAttribute) { Editable = InvalidAttribute });
        Changed?.Invoke();
    }

    private static EditFrame WrapField(View field, int y, Dim height)
    {
        // Same "TextField/TextView/DropDownList only paint under their own content" reasoning as
        // every dialog this section replaces - EditFrame's own fill covers the rest of the field
        // regardless of content.
        var background = field.GetAttributeForRole(VisualRole.Editable).Background;
        return new EditFrame(field) {
            X = 0, Y = y, Width = Dim.Fill(), Height = height,
            InnerBackgroundNormal = background, InnerBackgroundFocused = background,
        };
    }
}
