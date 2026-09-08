using System.Collections.ObjectModel;
using lazynats.Payloads;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Components;

// The adaptive-width/switchable-presentation/scrollable payload section shared by
// MessageDetailDialog and the KV Value Detail dialog - extracted so both compose the same
// width/focus/scroll invariants instead of re-implementing them a second time. See
// openspec/changes/kv-value-peek-and-view/design.md Decision 2.
//
// CanFocus=true with no focusable content of its own (the presentation dropdown starts
// CanFocus=false, same as every Label here) - the same "container becomes the fallback focus
// target" mechanic EditFrame relies on for its own pass-through focus (see EditFrame's own
// CanFocus comment: a CanFocus=true container with no focusable descendant becomes the focus
// target itself rather than leaving focus to fall through further). With nothing else focusable
// in the owning dialog, this section is what the dialog's focus resolves to by default, which is
// what lets this section's own scroll/selector-return key bindings below actually receive keys
// through Terminal.Gui's "recurse into the focused SubView first" NewKeyDownEvent order. The
// owning dialog's own Key.V binding (see OpenSelector) still fires regardless - an unhandled key
// at this section bubbles back up to the dialog's own KeyBindings once this section's own
// commands miss.
internal sealed class PayloadDetailSection: View, IShortcutSource
{
    private const int MaxPayloadVisibleLines = 16;

    // Wide enough for "Base64" (the longest option) plus the dropdown's own button glyph.
    private const int PresentationDropDownWidth = 10;

    // Reserved unconditionally from the label's measured width - whether the vertical scrollbar
    // actually ends up shown depends on the rendered line count, which itself depends on this
    // same width, so there's no non-circular way to reserve it only when needed. Wasting columns
    // when no scrollbar appears is preferable to overflowing under one when it does.
    private const int ScrollbarWidth = 1;

    // A blank column between the rendered content and the (reserved-for) scrollbar - without it,
    // content runs right up to the scrollbar's own column with no breathing room, which reads as
    // crowded even though nothing is actually clipped or overlapping.
    private const int ScrollbarGap = 1;

    private readonly byte[] _data;
    private readonly string _emptyText;
    private readonly bool _hasPayload;
    private readonly PayloadType[] _allowedPresentationTypes;
    private readonly PayloadType _defaultType;
    private readonly EditFrame _frame;
    private readonly Label _payloadView;

    // Null when the payload is empty (no selector is added at all - see MeasureAndRender's
    // hasPayload branch). Not focused by default (CanFocus starts false where it's constructed) -
    // stays out of the way until explicitly summoned via OpenSelector.
    private DropDownList? _presentationDropDown;

    // Fixed once MeasureAndRender runs, from the default type's line count - switching
    // presentation never resizes the frame, only what fits inside it before scrolling kicks in.
    private int _payloadVisibleLines;

    // The label's resolved available width, computed once (in MeasureAndRender) from its own
    // EditFrame after the owner's initial layout and reused for every Hex/Base64/Text render for
    // this section's lifetime - never recomputed on a later terminal resize. See
    // openspec/changes/adaptive-payload-width/design.md Decision 2.
    private int _labelWidth;

    // `contentKind` lets a caller that already classified this data elsewhere (e.g.
    // MessageDetailDialog reusing FeedEnvelope.CachedContentKind) skip a redundant probe pass;
    // null has this section classify `data` itself.
    public PayloadDetailSection(byte[] data, string label, string emptyText, PayloadContentKind? contentKind = null)
    {
        CanFocus = true;
        Width = Dim.Fill();

        _data = data;
        _emptyText = emptyText;
        _hasPayload = data.Length > 0;
        var kind = _hasPayload ? contentKind ?? PayloadContentProbe.Classify(data) : default;
        _allowedPresentationTypes = _hasPayload ? PayloadPresentation.AllowedTypes(kind) : [];
        _defaultType = _hasPayload ? PayloadPresentation.DefaultType(kind) : default;

        var labelView = new Label { Text = label, X = 0, Y = 0 };

        // Placeholder content/height - the actual text and frame height depend on the label's own
        // resolved width (Hex/Base64 sizing), which isn't known until the owner's subtree has gone
        // through an initial layout pass - see MeasureAndRender.
        _frame = EditFrame.CreateReadOnly(string.Empty, 1, MaxPayloadVisibleLines + 2, out _payloadView);

        Add(labelView, _frame);
    }

    // Called by the owner right after the owner's own initial Layout() call, so this section's
    // label Viewport is resolved - mirrors MessageDetailDialog's original sequence exactly, just
    // relocated. See design.md Decision 2.
    public void MeasureAndRender()
    {
        // Measured before RefreshPayloadScrollState (below) ever turns the vertical scrollbar on,
        // so the raw Viewport.Width here doesn't yet reflect the column that scrollbar will claim
        // once the payload turns out taller than MaxPayloadVisibleLines - hence the unconditional
        // ScrollbarWidth reservation, not just an artifact of measurement order. ScrollbarGap is
        // reserved on top of that so content doesn't sit flush against the scrollbar column.
        _labelWidth = _payloadView.Viewport.Width - ScrollbarWidth - ScrollbarGap;

        var text = _hasPayload
            ? PayloadPresentation.Render(_data, _defaultType, _labelWidth)
            : _emptyText;
        var lineCount = CountLines(text);
        _payloadVisibleLines = Math.Min(lineCount, MaxPayloadVisibleLines);
        var frameHeight = _payloadVisibleLines + 2;

        _payloadView.Text = text;
        _frame.Height = frameHeight;
        // One row for the label above the frame, same layout MessageDetailDialog used inline.
        Height = frameHeight + 1;

        if (_hasPayload)
        {
            // Non-generic DropDownList with an explicit Source, not DropDownList<PayloadType> -
            // the generic form always populates every enum value from the type itself and has no
            // hook to restrict the list to _allowedPresentationTypes.
            //
            // CanFocus = false at construction - every other view here is CanFocus=false too
            // (this section itself is the sole focus target - see this class's own CanFocus
            // comment), so this dropdown would otherwise be the only genuinely focusable
            // descendant, turning a minor display toggle into the de-facto primary control the
            // moment the dialog opens. OpenSelector flips it back to true (and only then calls
            // SetFocus) the moment V is actually pressed, so the dropdown is reached deliberately
            // instead of by default.
            // Pos.AnchorEnd() (no offset) is the width-aware form - it tracks the dropdown's own
            // Width and flushes its right edge against this section's own right edge, rather than
            // sitting immediately after the label.
            var presentationDropDown = new DropDownList {
                Source = new ListWrapper<PayloadType>(new ObservableCollection<PayloadType>(_allowedPresentationTypes)),
                ReadOnly = true,
                Text = _defaultType.ToString(),
                X = Pos.AnchorEnd(), Y = 0, Width = PresentationDropDownWidth,
                CanFocus = false,
            };
            Theme.ApplyEditableScheme(presentationDropDown);
            presentationDropDown.ValueChanged += (_, e) => OnPresentationChanged(e.NewValue);
            Add(presentationDropDown);
            _presentationDropDown = presentationDropDown;
        }

        BindScrollKeys(_payloadView);
        RefreshPayloadScrollState(lineCount);
    }

    // The owning dialog's own Key.V binding calls this (see design.md Decision 2's "Key.V stays
    // an owner-dialog concern" reasoning) - this section is never itself the thing that binds
    // Key.V, only what that binding invokes.
    public void OpenSelector()
    {
        if (_presentationDropDown is not { } dropDown) return;
        // CanFocus flips true here, not at construction - SetFocus() would otherwise be a no-op
        // (View.SetFocus() declines on a CanFocus=false view).
        dropDown.CanFocus = true;
        dropDown.SetFocus();
        dropDown.InvokeCommand(Command.Toggle);
    }

    // Empty when the payload is empty (no dropdown exists to advertise) - IShortcutSource is
    // opt-in per-hint, not per-view.
    public IEnumerable<ShortcutHint> Shortcuts =>
        _presentationDropDown is null ? [] : [new ShortcutHint(Key.V, "Presentation", OpenSelector)];

    // The dropdown's underlying value is its Text (ListWrapper<PayloadType> renders items via
    // PayloadType.ToString(), e.g. "Json"/"Hex") - GetCurrentSelectedIndex/SelectItemAtIndex on
    // the non-generic DropDownList are private, so selection is read back by parsing the new Text
    // rather than by index (still never touching _data - only _payloadView's own content).
    private void OnPresentationChanged(string? newValue)
    {
        if (newValue is null) return;
        var type = Enum.Parse<PayloadType>(newValue);
        var text = PayloadPresentation.Render(_data, type, _labelWidth);
        _payloadView.Text = text;
        RefreshPayloadScrollState(CountLines(text));

        // Mirrors OpenSelector in reverse: SetFocus() on this section first, then CanFocus=false
        // on the dropdown - moving focus away before revoking CanFocus, not after, since the
        // dropdown still holds focus at this point (DropDownList returns focus to itself once its
        // popover closes) and dropping CanFocus out from under the currently-focused view is the
        // wrong order to rely on. A selection is a one-shot action, not the start of an editing
        // session - leaving focus (and CanFocus) on the dropdown afterward would leave it right
        // back in the "de-facto primary control" state the OpenSelector/CanFocus design above
        // exists to avoid.
        if (_presentationDropDown is { } dropDown)
        {
            SetFocus();
            dropDown.CanFocus = false;
        }
    }

    // Toggles scroll state for the current content, and resets the viewport back to the top - a
    // prior scroll position from a taller presentation would otherwise leave a shorter one's view
    // showing blank space. Never touches key/command bindings - see BindScrollKeys.
    private void RefreshPayloadScrollState(int lineCount)
    {
        if (lineCount > _payloadVisibleLines)
        {
            _payloadView.SetContentHeight(lineCount);
            _payloadView.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        }
        else
        {
            _payloadView.ViewportSettings &= ~ViewportSettingsFlags.HasVerticalScrollBar;
            _payloadView.SetContentHeight(null);
        }

        var viewport = _payloadView.Viewport;
        _payloadView.Viewport = new System.Drawing.Rectangle(viewport.X, 0, viewport.Width, viewport.Height);
    }

    // Bound on this section, not the Label instance - AddCommand is protected on View, so only a
    // subclass (this section) can call it on itself. Called exactly once from MeasureAndRender,
    // regardless of whether the initial content overflows - KeyBindings.Add throws if the same
    // key/command pair is bound twice.
    private void BindScrollKeys(Label view)
    {
        AddCommand(Command.ScrollUp, () => { view.ScrollVertical(-1); return true; });
        AddCommand(Command.ScrollDown, () => { view.ScrollVertical(1); return true; });
        AddCommand(Command.PageUp, () => { view.ScrollVertical(-view.Viewport.Height); return true; });
        AddCommand(Command.PageDown, () => { view.ScrollVertical(view.Viewport.Height); return true; });
        KeyBindings.Add(Key.CursorUp, Command.ScrollUp);
        KeyBindings.Add(Key.CursorDown, Command.ScrollDown);
        KeyBindings.Add(Key.PageUp, Command.PageUp);
        KeyBindings.Add(Key.PageDown, Command.PageDown);
    }

    private static int CountLines(string text) => text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1;
}
