using System.Text;
using System.Text.Json;
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
internal sealed class MessageDetailDialog: Dialog
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

    public MessageDetailDialog(FeedEnvelope envelope)
    {
        // IApplication.Screen, not the obsolete static Application.Screen - see CLAUDE.md's DI
        // convention (Services.Root.GetRequiredService<T>(), not a static/ambient accessor).
        var app = Services.Root.GetRequiredService<IApplication>();

        Title = " Message ";
        Width = Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin));
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var message = envelope.Message;

        var headersText = FormatHeaders(message.Headers);
        var headerLineCount = CountLines(headersText);
        var headerFrameHeight = headerLineCount + 2;

        var payloadText = FormatPayload(message.Data ?? []);
        var payloadLineCount = CountLines(payloadText);
        var payloadVisibleLines = Math.Min(payloadLineCount, MaxPayloadVisibleLines);
        var payloadFrameHeight = payloadVisibleLines + 2;

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

        var payloadLabel = new Label { Text = "Payload", X = 0, Y = payloadLabelY };
        var payloadFrame = WrapText(payloadText, payloadFrameY, payloadFrameHeight, out var payloadView);

        if (payloadLineCount > payloadVisibleLines)
            WireScrolling(payloadView, payloadLineCount);

        Add(subjectLabel, subjectFrame, headersLabel, headerFrame, payloadLabel, payloadFrame);
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
    // (same reasoning as ObjectFileDialog's F2/Browse binding). Only wired when the payload
    // actually overflows its capped frame - see the constructor's guard.
    private void WireScrolling(Label view, int lineCount)
    {
        view.SetContentHeight(lineCount);
        view.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;

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

    private static string FormatPayload(byte[] data)
    {
        if (data.Length == 0) return "(empty payload)";

        return PayloadContentProbe.Classify(data) switch
        {
            PayloadContentKind.Json => FormatJson(data),
            PayloadContentKind.Utf8Text => Encoding.UTF8.GetString(data),
            _ => FormatHex(data),
        };
    }

    // Re-serializes with indentation rather than displaying the decoded text as-is, so a minified
    // wire payload (no insignificant whitespace) still reads as structured JSON - see design.md
    // Decision 2.
    // JsonElement.WriteTo, not JsonSerializer.Serialize(document.RootElement, ...) - the latter is
    // reflection-based (RequiresUnreferencedCode/RequiresDynamicCode) and unsafe under PublishAot
    // trimming, per CLAUDE.md; WriteTo needs neither.
    private static string FormatJson(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        using var document = JsonDocument.Parse(text);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            document.RootElement.WriteTo(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // 16 bytes per row, space-separated - enough to eyeball structure/length without an
    // offset column or ASCII gutter; see design.md Decision 3.
    private static string FormatHex(byte[] data)
    {
        var builder = new StringBuilder();
        for (var offset = 0; offset < data.Length; offset += 16)
        {
            var end = Math.Min(offset + 16, data.Length);
            for (var i = offset; i < end; i++)
            {
                if (i > offset) builder.Append(' ');
                builder.Append(data[i].ToString("X2"));
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
