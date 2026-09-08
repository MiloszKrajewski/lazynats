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
// one fixed-height scrollable block for everything - see design.md Decision 2's redesign. The
// payload section's adaptive-width/switchable-presentation/scrollable mechanics are shared with
// the KV Value Detail dialog via PayloadDetailSection - see
// openspec/changes/kv-value-peek-and-view/design.md Decision 2. Dialog.Height is left unset (its
// own Dim.Auto default) so the whole dialog grows to fit, still bounded by Terminal.Gui's
// built-in screen-percentage clamp for the pathological case.
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

    private readonly PayloadDetailSection _payloadSection;

    public MessageDetailDialog(FeedEnvelope envelope)
    {
        // IApplication.Screen, not the obsolete static Application.Screen - see CLAUDE.md's DI
        // convention (Services.Root.GetRequiredService<T>(), not a static/ambient accessor).
        var app = Services.Root.GetRequiredService<IApplication>();

        Title = " Message ";
        Width = Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin));
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var message = envelope.Message;
        var payloadData = message.Data ?? [];

        var headersText = FormatHeaders(message.Headers);
        var headerLineCount = CountLines(headersText);
        var headerFrameHeight = headerLineCount + 2;

        const int subjectY = 1;
        const int subjectFrameHeight = 3;
        var headersLabelY = subjectY + subjectFrameHeight;
        var headerFrameY = headersLabelY + 1;
        var payloadSectionY = headerFrameY + headerFrameHeight;

        var subjectLabel = new Label { Text = "Subject", X = 0, Y = 0 };
        var subjectFrame = EditFrame.CreateReadOnly(message.Subject, subjectY, subjectFrameHeight, out var subjectView);
        // Subject reads first and identifies the message - cyan foreground picks it out from the
        // plain white Headers/Payload text below, both still on the same editable-grey background.
        subjectView.SetScheme(new Scheme(new Attribute(Theme.SubjectColor, Theme.EditableBackground)));

        var headersLabel = new Label { Text = "Headers", X = 0, Y = headersLabelY };
        var headerFrame = EditFrame.CreateReadOnly(headersText, headerFrameY, headerFrameHeight, out _);

        // Reuses envelope.CachedContentKind (populated by FeedRowFormatter once the row is
        // rendered, which it always has been by the time this dialog can be opened from it)
        // rather than reclassifying from scratch, avoiding a redundant probe pass over the same
        // bytes.
        var contentKind = payloadData.Length > 0
            ? envelope.CachedContentKind ??= PayloadContentProbe.Classify(payloadData)
            : (PayloadContentKind?)null;
        _payloadSection = new PayloadDetailSection(payloadData, "Payload", "(empty payload)", contentKind) {
            X = 0, Y = payloadSectionY,
        };

        Add(subjectLabel, subjectFrame, headersLabel, headerFrame, _payloadSection);

        // Forces an immediate layout pass so the payload section's label Viewport - and therefore
        // its resolved available width - is known synchronously, without waiting for this dialog's
        // eventual app.Run to render it. Safe to call before app.Run: the Dialog's own Width is a
        // self-contained Dim.Func over app.Screen.Width, with no SuperView dependency.
        Layout();
        _payloadSection.MeasureAndRender();

        // A modal Dialog is its own top-level with no SuperView link back to MainWindow (same
        // point MainWindow's own `?` binding comment makes), so MainWindow's global `?`
        // KeyDown handler never sees a keypress made while this dialog is open - confirmed via
        // tmux, `?` was a no-op here before this binding existed. ShortcutPickerLauncher.BindKey
        // owns the shared deferred-collect-run-invoke sequence, including picking its own start
        // view (see BindKey/ResolveStartView), which resolves to `_payloadSection` itself (the
        // dialog's sole CanFocus=true descendant - see PayloadDetailSection's own CanFocus
        // comment) - so ShortcutAggregator's walk finds `_payloadSection`'s own
        // IShortcutSource.Shortcuts (today, only the V/Presentation hint) directly. This dialog
        // deliberately does NOT also implement IShortcutSource itself and re-forward to
        // `_payloadSection.Shortcuts` - confirmed via tmux that doing so double-lists the same
        // hint, since the aggregator's walk already passes through `_payloadSection` on its way
        // up to this dialog.
        ShortcutPickerLauncher.BindKey(this);

        // Command.Expand ("Expands a list or item") rather than a bare unbound handler - matches
        // this codebase's existing pattern of driving dialog-level shortcuts through
        // AddCommand/KeyBindings, and Command semantics keep the binding's intent explicit even
        // though it's read back only via this Key.V binding. Key.V stays an owner-dialog concern
        // (see design.md Decision 2) - _payloadSection.OpenSelector() itself no-ops when the
        // payload is empty (no dropdown was built), so this binding is harmless either way.
        AddCommand(Command.Expand, () => { _payloadSection.OpenSelector(); return true; });
        KeyBindings.Add(Key.V, Command.Expand);
    }

    // Confirming the presentation dropdown's highlighted item (Enter, closing its popup) reaches
    // here as an unhandled Accept bubbling up from the child - same reasoning as PublishDialog's
    // identical override (its Subject/Headers Enter). Left unhandled, Dialog's default "unhandled
    // Accept -> RequestStop" would close this whole dialog on every presentation change instead of
    // just updating the selection; Esc remains the only way to close it (per this dialog's own
    // buttonless-dialog convention).
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static int CountLines(string text) => text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1;

    // One `Key: Value` line per header, or an explicit "(no headers)" line - see
    // message-detail-dialog spec's empty-state scenario.
    private static string FormatHeaders(NatsHeaders? headers) =>
        headers is { Count: > 0 }
            ? string.Join('\n', headers.Select(header => $"{header.Key}: {header.Value}"))
            : "(no headers)";
}
