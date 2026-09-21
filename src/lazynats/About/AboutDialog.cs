using lazynats.Components;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.About;

// Plain Dialog (not Dialog<T>): nothing outside needs a value back once it closes, same reasoning
// as MessageDetailDialog/PublishDialog. Buttonless - Esc closes it via the inherited
// buttonless-dialog cancellation convention shared with PatternDialog/MessageDetailDialog. Fixed
// width (not Dim.Func like MessageDetailDialog - this dialog has no reason to want extra
// horizontal room the way JSON payloads do), height left as Dim.Auto's own default. Scroll keys
// are bound directly on the dialog itself rather than extracting a PayloadDetailSection-style
// helper type - there's exactly one text block here and no presentation dropdown to warrant it -
// see design.md Decisions.
internal sealed class AboutDialog: Dialog
{
    private const int DialogWidth = 60;
    private const int MaxVisibleLines = 10;

    public AboutDialog()
    {
        Title = " About ";
        Width = DialogWidth;
        Padding.Thickness = new Thickness(1, 0, 1, 0);

        var text = AboutText.Load();
        var lineCount = CountLines(text);
        var visibleLines = Math.Min(lineCount, MaxVisibleLines);
        var frameHeight = visibleLines + 2;

        var frame = EditFrame.CreateReadOnly(text, 0, frameHeight, out var view);
        Add(frame);

        if (lineCount > visibleLines)
        {
            view.SetContentHeight(lineCount);
            view.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        }

        BindScrollKeys(view);
    }

    // Mirrors PayloadDetailSection.BindScrollKeys - same four-command shape, just bound on this
    // dialog directly instead of a separate section, since About has only the one text block.
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
