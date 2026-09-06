using Terminal.Gui.Editor;

namespace lazynats.AotProbe.Probes;

internal static class EditorProbe
{
    public static Task Run()
    {
        var editor = new Editor { Text = "hello" };

        if (editor.Text != "hello")
            throw new InvalidOperationException($"Expected Text to round-trip as 'hello', got '{editor.Text}'.");

        return Task.CompletedTask;
    }
}
