using lazynats.Components;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Values;

// Read-only detail view for a single KV entry, opened via `V` from the key-level list (see
// ValuesTab.OpenValueDetailDialog) - mirrors MessageDetailDialog's post-PayloadDetailSection-
// extraction shape (see openspec/changes/kv-value-peek-and-view/design.md Decisions 2 and 4).
// Plain Dialog (not Dialog<T>), same reasoning as PublishDialog: nothing outside the dialog needs
// a value back once it closes - this is purely a viewer. Buttonless - Esc closes via the same
// inherited cancellation convention as every other buttonless dialog here.
//
// Three sections, mirroring MessageDetailDialog's Subject/Headers/Payload shape exactly rather
// than one combined "Details" frame (see design.md Decision 4's revision): a "Key" frame holding
// just the bare key text (cyan, Theme.SubjectColor - same role Subject plays for a message), a
// "Metadata" frame of Bucket/Revision/Created/Operation as Label: Value lines (LimeGreen,
// Theme.HeaderColor - same role Headers plays for a message), and a PayloadDetailSection labeled
// "Value" over the entry's value bytes. Dialog.Height is left unset (its own Dim.Auto default) so
// the whole dialog grows to fit, same as MessageDetailDialog.
internal sealed class ValueDetailDialog: Dialog
{
    // Same reasoning/values as MessageDetailDialog's own consts - see that class's comment.
    private const int PreferredDialogWidth = 132;
    private const int TerminalWidthMargin = 4;

    private readonly PayloadDetailSection _payloadSection;

    public ValueDetailDialog(string bucket, KeyDetails.Entry entry)
    {
        // IApplication.Screen, not the obsolete static Application.Screen - see CLAUDE.md's DI
        // convention (Services.Root.GetRequiredService<T>(), not a static/ambient accessor).
        var app = Services.Root.GetRequiredService<IApplication>();

        Title = " Value ";
        Width = Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin));
        Padding.Thickness = new Thickness(1, 1, 1, 0);

        var metadataText = FormatMetadata(bucket, entry);
        var metadataLineCount = CountLines(metadataText);
        var metadataFrameHeight = metadataLineCount + 2;

        const int keyY = 1;
        const int keyFrameHeight = 3;
        var metadataLabelY = keyY + keyFrameHeight;
        var metadataFrameY = metadataLabelY + 1;
        var payloadSectionY = metadataFrameY + metadataFrameHeight;

        var keyLabel = new Label { Text = "Key", X = 0, Y = 0 };
        var keyFrame = EditFrame.CreateReadOnly(entry.Key, keyY, keyFrameHeight, out var keyView);
        // Mirrors MessageDetailDialog's subjectView coloring - cyan picks the key out from the
        // plain white metadata/value text below, both still on the same editable-grey background.
        keyView.SetScheme(new Scheme(new Attribute(Theme.SubjectColor, Theme.EditableBackground)));

        var metadataLabel = new Label { Text = "Metadata", X = 0, Y = metadataLabelY };
        var metadataFrame = EditFrame.CreateReadOnly(metadataText, metadataFrameY, metadataFrameHeight, out var metadataView);
        // LimeGreen (Theme.HeaderColor) - same color the live feed uses for a message's own
        // header segments, so a KV entry's metadata reads with the same accent role.
        metadataView.SetScheme(new Scheme(new Attribute(Theme.HeaderColor, Theme.EditableBackground)));

        _payloadSection = new PayloadDetailSection(entry.Value, "Value", "(empty value)") {
            X = 0, Y = payloadSectionY,
        };

        Add(keyLabel, keyFrame, metadataLabel, metadataFrame, _payloadSection);

        // Forces an immediate layout pass so the payload section's label Viewport - and therefore
        // its resolved available width - is known synchronously, without waiting for this
        // dialog's eventual app.Run to render it. Safe to call before app.Run: the Dialog's own
        // Width is a self-contained Dim.Func over app.Screen.Width, with no SuperView dependency.
        Layout();
        _payloadSection.MeasureAndRender();

        // A modal Dialog is its own top-level with no SuperView link back to MainWindow, so
        // MainWindow's global `?` KeyDown handler never sees a keypress made while this dialog is
        // open. ShortcutPickerLauncher.BindKey owns the shared deferred-collect-run-invoke
        // sequence, including picking its own start view (see BindKey/ResolveStartView), which
        // resolves to `_payloadSection` itself (this dialog's sole CanFocus=true descendant - see
        // PayloadDetailSection's own CanFocus comment). This dialog deliberately does NOT also
        // implement IShortcutSource itself and re-forward to `_payloadSection.Shortcuts` - see
        // ShortcutPickerLauncher.ResolveStartView's own comment on why that would double-list the
        // same hint (confirmed via tmux for MessageDetailDialog's identical shape).
        ShortcutPickerLauncher.BindKey(this);

        // Command.Expand ("Expands a list or item") rather than a bare unbound handler - matches
        // this codebase's existing pattern of driving dialog-level shortcuts through
        // AddCommand/KeyBindings. Key.V stays an owner-dialog concern (see design.md Decision 2) -
        // _payloadSection.OpenSelector() itself no-ops when the value is empty (no dropdown was
        // built), so this binding is harmless either way.
        AddCommand(Command.Expand, () => { _payloadSection.OpenSelector(); return true; });
        KeyBindings.Add(Key.V, Command.Expand);
    }

    // Confirming the presentation dropdown's highlighted item (Enter, closing its popup) reaches
    // here as an unhandled Accept bubbling up from the child. Left unhandled, Dialog's default
    // "unhandled Accept -> RequestStop" would close this whole dialog on every presentation change
    // instead of just updating the selection; Esc remains the only way to close it.
    protected override bool OnAccepting(CommandEventArgs args) => true;

    private static int CountLines(string text) => text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1;

    // One `Label: Value` line per field, mirroring MessageDetailDialog.FormatHeaders' formatting -
    // see design.md Decision 4. Key itself is excluded - it has its own section above.
    private static string FormatMetadata(string bucket, KeyDetails.Entry entry) => string.Join('\n', [
        $"Bucket: {bucket}",
        $"Revision: {entry.Revision}",
        $"Created: {entry.Created}",
        $"Operation: {entry.Operation}",
    ]);
}
