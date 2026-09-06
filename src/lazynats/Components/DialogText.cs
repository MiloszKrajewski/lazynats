namespace lazynats.Components;

// Modal-dialog counterpart to the "leading/trailing space in a bordered container's title"
// convention (CLAUDE.md: e.g. " Live Feed " in MainWindow.cs) - applied to both title and
// body/message text here since MessageBox has no Padding.Thickness of its own to lean on the way
// a Dialog<T> subclass does, and auto-sizes tightly around whichever text is widest.
internal static class DialogText
{
    public static string Pad(string text) => $" {text} ";
}
