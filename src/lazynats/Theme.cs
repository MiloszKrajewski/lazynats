using Terminal.Gui.Drawing;

namespace lazynats;

// Single declared place for the app's tunable theme colors (see openspec/changes/add-dark-theme).
// Swap a value here to experiment with alternatives - every dependent (Program.cs's Base/Dialog
// scheme overrides, and the various invalid-input highlight Attributes in PublishView/HeaderDialog/
// PatternDialog) reads from here instead of repeating the literal.
internal static class Theme
{
    // Darker than ColorName16.DarkGray (118,118,118) so field text stays legible without the
    // control looking "shiny". On a terminal without truecolor support this falls back to
    // ColorName16.Black, not DarkGray: Color.GetClosestNamedColor16() confirms (32,32,32) is
    // nearer Black than any other 16-color palette entry.
    public static readonly Color EditableBackground = new(32, 32, 32);
}
