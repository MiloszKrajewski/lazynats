using System.Collections.ObjectModel;
using System.Text;
using lazynats.Components;
using lazynats.Payloads;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.LiveFeed;

// Read-only detail view for a single feed message, replacing MainWindow's old
// `MessageBox.Query(...)` stub that showed only the subject. Plain Dialog (not Dialog<T>), same
// reasoning as PublishDialog: nothing outside the dialog needs a value back once it closes - this
// is purely a viewer. Buttonless - Esc closes via the same inherited cancellation convention as
// every other buttonless dialog here (ShortcutPickerDialog, PatternDialog).
//
// Subject, headers, and payload each get their own EditFrame (mirroring PublishDialog's
// Subject/Headers/Payload layout, just read-only) sized to that section's own content instead of
// one fixed-height scrollable block for everything - see design.md Decision 2's redesign. Only
// the payload frame is capped (MaxPayloadVisibleLines) with its own scrollbar, since subject is
// always one line and headers are normally few; Dialog.Height is left unset (its own Dim.Auto
// default) so the whole dialog grows to fit, still bounded by Terminal.Gui's built-in
// screen-percentage clamp for the pathological case.
internal sealed class MessageDetailDialog: Dialog, IShortcutSource
{
    // Prefer a wide dialog (payloads are often JSON that benefits from horizontal room), but never
    // wider than the terminal itself - Dim.Func re-evaluates on every layout pass, so resizing the
    // terminal (or opening this on a smaller one) reflows the dialog instead of clipping/crashing.
    // TerminalWidthMargin leaves a couple of columns of breathing room on each side rather than
    // running the dialog edge-to-edge with the screen. Height has no such cap: it's left to
    // Dim.Auto below, which already sizes from content.
    private const int PreferredDialogWidth = 132;
    private const int TerminalWidthMargin = 4;
    private const int MaxPayloadVisibleLines = 16;

    // Wide enough for "Base64" (the longest option) plus the dropdown's own button glyph.
    private const int PresentationDropDownWidth = 10;

    // Reserved unconditionally from the payload label's measured width (see the
    // _payloadLabelWidth comment below) - whether the vertical scrollbar actually ends up shown
    // depends on the rendered line count, which itself depends on this same width, so there's no
    // non-circular way to reserve it only when needed. Wasting columns when no scrollbar appears
    // is preferable to overflowing under one when it does.
    private const int ScrollbarWidth = 1;

    // A blank column between the rendered content and the (reserved-for) scrollbar - without it,
    // content runs right up to the scrollbar's own column with no breathing room, which reads as
    // crowded even though nothing is actually clipped or overlapping.
    private const int ScrollbarGap = 1;

    private readonly byte[] _payloadData;
    private readonly PayloadType[] _allowedPresentationTypes;
    private readonly Label _payloadView;

    // Null when the payload is empty (no selector is added at all - see the hasPayload branch
    // below). Not focused by default (CanFocus starts false where it's constructed) - this
    // dialog is read-only and mostly viewed, not driven through this one control, so it stays
    // out of the way until explicitly summoned via the V shortcut (see
    // Shortcuts/OpenPresentationSelector).
    private DropDownList? _presentationDropDown;

    // Fixed at construction from the default type's line count (design.md Decision 4) - switching
    // presentation never resizes the frame, only what fits inside it before scrolling kicks in.
    private readonly int _payloadVisibleLines;

    // The payload Label's resolved available width, computed once from its own EditFrame after the
    // dialog's initial layout and reused for every Hex/Base64 render for the dialog's lifetime -
    // never recomputed on a later terminal resize. See
    // openspec/changes/adaptive-payload-width/design.md Decision 2.
    private readonly int _payloadLabelWidth;

    public MessageDetailDialog(FeedEnvelope envelope)
    {
        // IApplication.Screen, not the obsolete static Application.Screen - see CLAUDE.md's DI
        // convention (Services.Root.GetRequiredService<T>(), not a static/ambient accessor).
        var app = Services.Root.GetRequiredService<IApplication>();

        Title = " Message ";
        Width = Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin));
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var message = envelope.Message;
        _payloadData = message.Data ?? [];

        var headersText = FormatHeaders(message.Headers);
        var headerLineCount = CountLines(headersText);
        var headerFrameHeight = headerLineCount + 2;

        // Empty payload bypasses the presentation concept entirely - there's nothing to classify
        // or switch between, so no selector is offered (unchanged from prior behavior).
        var hasPayload = _payloadData.Length > 0;
        var contentKind = hasPayload ? PayloadContentProbe.Classify(_payloadData) : default;
        _allowedPresentationTypes = hasPayload ? PayloadPresentation.AllowedTypes(contentKind) : [];
        var defaultType = hasPayload ? PayloadPresentation.DefaultType(contentKind) : default;

        const int subjectY = 1;
        const int subjectFrameHeight = 3;
        var headersLabelY = subjectY + subjectFrameHeight;
        var headerFrameY = headersLabelY + 1;
        var payloadLabelY = headerFrameY + headerFrameHeight;
        var payloadFrameY = payloadLabelY + 1;

        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        var subjectFrame = WrapText(message.Subject, subjectY, subjectFrameHeight, out var subjectView);
        // Subject reads first and identifies the message - cyan foreground picks it out from the
        // plain white Headers/Payload text below, both still on the same editable-grey background.
        subjectView.SetScheme(new Scheme(new Attribute(ColorName16.Cyan, Theme.EditableBackground)));

        var headersLabel = new Label { Text = "Headers", X = 0, Y = headersLabelY };
        var headerFrame = WrapText(headersText, headerFrameY, headerFrameHeight, out _);

        // Placeholder content/height - the payload EditFrame's actual text and height depend on
        // its own resolved width (Hex/Base64 sizing), which isn't known until this dialog's
        // subtree has gone through an initial layout pass, just below.
        var payloadLabel = new Label { Text = "Payload", X = 0, Y = payloadLabelY };
        var payloadFrame = WrapText(string.Empty, payloadFrameY, MaxPayloadVisibleLines + 2, out _payloadView);

        Add(subjectLabel, subjectFrame, headersLabel, headerFrame, payloadLabel, payloadFrame);

        // Forces an immediate layout pass so the payload Label's Viewport - and therefore its
        // resolved available width - is known synchronously, without waiting for this dialog's
        // eventual app.Run to render it. Safe to call before app.Run: the Dialog's own Width is a
        // self-contained Dim.Func over app.Screen.Width, with no SuperView dependency.
        Layout();
        // Measured before RefreshPayloadScrollState (below) ever turns the vertical scrollbar on,
        // so the raw Viewport.Width here doesn't yet reflect the column that scrollbar will claim
        // once the payload turns out taller than MaxPayloadVisibleLines - hence the unconditional
        // ScrollbarWidth reservation, not just an artifact of measurement order. ScrollbarGap is
        // reserved on top of that so content doesn't sit flush against the scrollbar column.
        _payloadLabelWidth = _payloadView.Viewport.Width - ScrollbarWidth - ScrollbarGap;

        var payloadText = hasPayload
            ? PayloadPresentation.Render(_payloadData, defaultType, _payloadLabelWidth)
            : "(empty payload)";
        var payloadLineCount = CountLines(payloadText);
        _payloadVisibleLines = Math.Min(payloadLineCount, MaxPayloadVisibleLines);
        var payloadFrameHeight = _payloadVisibleLines + 2;

        _payloadView.Text = payloadText;
        payloadFrame.Height = payloadFrameHeight;

        // Bound once, unconditionally - KeyBindings.Add throws if the same key/command pair is
        // added twice, so this can't be re-run from RefreshPayloadScrollState on every
        // presentation switch (design.md Decision 4). Harmless when the current text fits: with
        // ViewportSettingsFlags.HasVerticalScrollBar off and content height tracking the viewport,
        // ScrollVertical has nothing to scroll to.
        BindScrollKeys(_payloadView);

        // A modal Dialog is its own top-level with no SuperView link back to MainWindow (same
        // point MainWindow's own `?` binding comment makes), so MainWindow's global `?`
        // KeyDown handler never sees a keypress made while this dialog is open - confirmed via
        // tmux, `?` was a no-op here before this binding existed. Mirrors MainWindow's own `?`
        // handling almost exactly: same ShortcutAggregator/ShortcutPickerDialog pair, same
        // AddTimeout(Zero, ...) deferral so the nested App.Run doesn't re-enter this
        // still-unwinding key dispatch, and - like MainWindow - a raw KeyDown subscription
        // rather than AddCommand/KeyBindings(Command.Context): confirmed via tmux that
        // Command.Context specifically never reaches a custom handler here (Terminal.Gui's
        // built-in "open context/popover menu" semantics for that command appear to take over
        // first), the same reason MainWindow's own `?` avoids AddCommand/KeyBindings entirely.
        // Scoped to this dialog's own focused-chain shortcuts (today, only the V/Presentation
        // hint from Shortcuts below) rather than the app's top-level ones. Fires regardless of
        // hasPayload - an empty-payload message correctly shows "no shortcuts" rather than the
        // key silently doing nothing, same as any other view with nothing to advertise.
        KeyDown += (_, key) => {
            if (key != new Key('?')) return;
            key.Handled = true;
            app.AddTimeout(TimeSpan.Zero, () => {
                // Collect(this) directly, not App.TopRunnableView?.MostFocused (MainWindow's own
                // form, which needs the generality of "start from whatever's actually focused,
                // app-wide") or this.MostFocused (confirmed via tmux to resolve, in this dialog's
                // no-explicit-focus starting state, to an internal adornment scaffolding View
                // that isn't a descendant reachable back to `this` via SuperView - both left the
                // picker empty). This dialog's own IShortcutSource is the only one anywhere in
                // its subtree, so starting the walk at `this` finds it unconditionally, in every
                // focus state, without needing to locate "whatever's currently focused" first.
                var hints = ShortcutAggregator.Collect(this);
                var picker = new ShortcutPickerDialog(hints);
                app.Run(picker);
                picker.Result?.Action();
                return false;
            });
        };

        if (hasPayload)
        {
            // Non-generic DropDownList with an explicit Source, not DropDownList<PayloadType> -
            // the generic form always populates every enum value from the type itself and has no
            // hook to restrict the list to _allowedPresentationTypes. See design.md Decision 3.
            //
            // CanFocus = false at construction - confirmed via tmux that TabStop = NoStop alone
            // is NOT enough here: TabStop only governs Tab-key cycling, not Terminal.Gui's
            // separate "no view has focus yet, fall back to the only CanFocus descendant"
            // activation-time behavior, which still lands on this dropdown even with
            // TabStop = NoStop (reproduced: pressing bare Down immediately after opening the
            // dialog - before ever pressing V - already cycled the presentation value, proving
            // the dropdown was focused from the start regardless of TabStop). Since every other
            // view in this dialog is CanFocus=false (WrapText's Labels), this dropdown would
            // otherwise be the dialog's *only* focus candidate at all, by either mechanism -
            // turning a minor display toggle into the de-facto primary control the moment the
            // dialog opens, not what a read-only viewer should default to. CanFocus=false removes
            // it from focus candidacy entirely; OpenPresentationSelector flips it back to true
            // (and only then calls SetFocus) the moment V is actually pressed, so the dropdown is
            // reached deliberately instead of by default - see design.md Decision 5.
            // Pos.AnchorEnd() (no offset) is the width-aware form - it tracks the dropdown's own
            // Width and flushes its right edge against the content area's right edge (inside
            // Padding, same edge Dim.Fill() targets on subjectFrame/headerFrame/payloadFrame
            // above), rather than sitting immediately after the Payload label.
            var presentationDropDown = new DropDownList {
                Source = new ListWrapper<PayloadType>(new ObservableCollection<PayloadType>(_allowedPresentationTypes)),
                ReadOnly = true,
                Text = defaultType.ToString(),
                X = Pos.AnchorEnd(), Y = payloadLabelY, Width = PresentationDropDownWidth,
                CanFocus = false,
            };
            Theme.ApplyEditableScheme(presentationDropDown);
            presentationDropDown.ValueChanged += (_, e) => OnPresentationChanged(e.NewValue);
            Add(presentationDropDown);
            _presentationDropDown = presentationDropDown;

            // Command.Expand ("Expands a list or item") rather than a bare unbound handler -
            // matches this codebase's existing pattern of driving dialog-level shortcuts through
            // AddCommand/KeyBindings (e.g. BindScrollKeys below), and Command semantics keep the
            // binding's intent explicit even though it's read back only via this Key.V binding.
            AddCommand(Command.Expand, () => { OpenPresentationSelector(); return true; });
            KeyBindings.Add(Key.V, Command.Expand);
        }

        RefreshPayloadScrollState(payloadLineCount);
    }

    // Confirming the presentation dropdown's highlighted item (Enter, closing its popup) reaches
    // here as an unhandled Accept bubbling up from the child - same reasoning as PublishDialog's
    // identical override (its Subject/Headers Enter). Left unhandled, Dialog's default "unhandled
    // Accept -> RequestStop" would close this whole dialog on every presentation change instead of
    // just updating the selection; Esc remains the only way to close it (per this dialog's own
    // buttonless-dialog convention).
    protected override bool OnAccepting(CommandEventArgs args) => true;

    // Empty when the payload is empty (no dropdown exists to advertise) - IShortcutSource is
    // opt-in per-hint, not per-view, so there's nothing wrong with a source that sometimes yields
    // nothing. Surfaced via ShortcutAggregator to both the `?` picker and (were this dialog to
    // gain a status bar of its own) any future status-bar sync - see design.md Decision 5.
    public IEnumerable<ShortcutHint> Shortcuts =>
        _presentationDropDown is null ? [] : [new ShortcutHint(Key.V, "Presentation", OpenPresentationSelector)];

    // Focuses the dropdown and immediately opens its popover in one step, rather than just
    // focusing it and leaving the user to press Space/F4/Alt+Down themselves - V is meant to read
    // as "jump straight to picking a presentation", not "jump to a control that then needs a
    // second keypress to do anything". InvokeCommand(Command.Toggle), not ToggleDropDown()/
    // OpenDropDown() - both are public in a newer Terminal.Gui than the 2.4.10 this project
    // targets (confirmed via reflection against the installed package); Command.Toggle is the
    // same command Space/F4/Alt+Down already invoke per DropDownList's own default key bindings,
    // so this reuses the framework's own "open/close" entry point instead of a version-specific
    // method.
    private void OpenPresentationSelector()
    {
        if (_presentationDropDown is not { } dropDown) return;
        // CanFocus flips true here, not at construction - see the CanFocus=false comment where
        // the dropdown is built; SetFocus() would otherwise be a no-op (View.SetFocus() declines
        // on a CanFocus=false view).
        dropDown.CanFocus = true;
        dropDown.SetFocus();
        dropDown.InvokeCommand(Command.Toggle);
    }

    // The dropdown's underlying value is its Text (ListWrapper<PayloadType> renders items via
    // PayloadType.ToString(), e.g. "Json"/"Hex") - GetCurrentSelectedIndex/SelectItemAtIndex on
    // the non-generic DropDownList are private, so selection is read back by parsing the new Text
    // rather than by index (still never touching envelope.Message - only this Label's content).
    private void OnPresentationChanged(string? newValue)
    {
        if (newValue is null) return;
        var type = Enum.Parse<PayloadType>(newValue);
        var text = PayloadPresentation.Render(_payloadData, type, _payloadLabelWidth);
        _payloadView.Text = text;
        RefreshPayloadScrollState(CountLines(text));

        // Mirrors OpenPresentationSelector in reverse: SetFocus() on the Dialog first, then
        // CanFocus = false on the dropdown - moving focus away before revoking CanFocus, not
        // after, since the dropdown still holds focus at this point (DropDownList returns focus
        // to itself once its popover closes, per its own docs) and dropping CanFocus out from
        // under the currently-focused view is the wrong order to rely on. A selection is a
        // one-shot action, not the start of an editing session - leaving focus (and CanFocus) on
        // the dropdown afterward would leave it right back in the "de-facto primary control"
        // state the V-shortcut/CanFocus=false design above exists to avoid (design.md Decision 5).
        if (_presentationDropDown is { } dropDown)
        {
            SetFocus();
            dropDown.CanFocus = false;
        }
    }

    // Toggles scroll state for the payload Label's current content, and resets the viewport back
    // to the top - a prior scroll position from a taller presentation would otherwise leave a
    // shorter one's view showing blank space. Never touches key/command bindings - see
    // BindScrollKeys. See design.md Decision 4.
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

    // Label's TextFormatter defaults to single-line (it's normally a hotkey caption) - opt into
    // multi-line rendering of the pre-formatted content, without word-wrap re-flowing lines
    // already deliberately laid out (the hex dump's fixed 16-bytes-per-row width). CanFocus=false
    // on the frame: unlike every other EditFrame in this codebase (which wraps a focusable
    // TextField/TextView), this one wraps a plain non-focusable Label - leaving the frame
    // focusable would pull Tab focus (and this dialog's own scroll key bindings) onto an empty
    // frame instead of staying on the Dialog itself, same "nothing here is ever focused
    // independently" reasoning as this dialog's original single-Label design.
    //
    // Theme.ApplyEditableScheme, not the Dialog's own plain-black Normal role: the Label itself
    // has to paint the same EditableBackground grey the EditFrame fills behind it, or its own
    // glyph backgrounds (painted per-character, wherever it has text) would mismatch the frame's
    // fill and read as a seam.
    private EditFrame WrapText(string text, int y, int height, out Label view)
    {
        // HotKeySpecifier must be disabled before Text is assigned - Label parses '_' out of Text
        // at assignment time using whatever HotKeySpecifier is current, and Label defaults it to
        // '_' (unlike the plain View base, which defaults to disabled). Message content is
        // arbitrary (NATS subjects/JSON/headers routinely contain '_'), so leaving the default on
        // would silently eat underscores and underline the following character instead of
        // rendering the text verbatim.
        view = new Label { HotKeySpecifier = (Rune)0xffff, Text = text };
        view.TextFormatter.MultiLine = true;
        view.TextFormatter.WordWrap = false;
        Theme.ApplyEditableScheme(view);

        var frame = new EditFrame(view) {
            X = 0, Y = y, Width = Dim.Fill(), Height = height,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        frame.CanFocus = false;
        return frame;
    }

    // Bound on the dialog itself, not the Label instance - AddCommand is protected on View, so
    // only a subclass (this dialog) can call it on itself; a plain Label instance is out of reach
    // (same reasoning as ObjectFileDialog's F2/Browse binding). Called exactly once from the
    // constructor, regardless of whether the initial content overflows - KeyBindings.Add throws if
    // the same key/command pair is bound twice, so this can't be re-run per presentation switch.
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

    // One `Key: Value` line per header, or an explicit "(no headers)" line - see
    // message-detail-dialog spec's empty-state scenario.
    private static string FormatHeaders(NatsHeaders? headers) =>
        headers is { Count: > 0 }
            ? string.Join('\n', headers.Select(header => $"{header.Key}: {header.Value}"))
            : "(no headers)";
}
