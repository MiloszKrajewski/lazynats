using Terminal.Gui.ViewBase;

namespace lazynats.Components;

// Workaround for a Terminal.Gui v2.4.10 redraw gap: TextField.Adjust() (and TextView's
// equivalent), called after every edit, only invokes SetNeedsDraw() when it judges scrolling is
// needed - otherwise it just repositions the terminal cursor via UpdateCursor(), leaving newly
// pasted content unpainted until some later edit forces a redraw. A single Ctrl+V paste that fits
// without scrolling hits that skipped branch: the model and cursor update correctly, but the
// screen doesn't, until the next keystroke's own redraw incidentally reveals it. Forcing a
// redraw unconditionally after every paste sidesteps that heuristic without touching the library.
internal static class ViewPasteExtensions
{
    public static void FixPasteRedraw(this View view) => view.Pasted += (_, _) => view.SetNeedsDraw();
}
