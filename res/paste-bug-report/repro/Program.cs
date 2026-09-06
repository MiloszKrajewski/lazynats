using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

var app = Application.Create();
app.Run<MainWindow>().Dispose();

// Minimal stand-in for the reporting app's own wrapper view (which is otherwise irrelevant to
// this bug - see ../report.md's "Relationship to the reporting app" section). Keeps only the one
// piece of shape that matters here: a wrapper with CanFocus=true and no focusable content of its
// own, so keyboard/mouse focus drills straight through to the single child it wraps, never
// stopping at the wrapper itself.
internal sealed class SimpleFrame: View
{
    public SimpleFrame(View child)
    {
        CanFocus = true;
        child.X = 0;
        child.Y = 0;
        child.Width = Dim.Fill();
        child.Height = Dim.Fill();
        Add(child);
    }
}

internal sealed class ReproDialog: Dialog
{
    public ReproDialog()
    {
        Title = " Paste Bug Repro ";

        var step1 = new Label {
            X = 0, Y = 0, Width = 70, Height = 2,
            Text = "When dialog is opened you can type, but you cannot paste (c-V)\neven with explicit SetFocus() call."
        };
        var fieldA = new TextField { Width = 70 };
        var frameA = new SimpleFrame(fieldA) { X = 0, Y = 3, Width = 70, Height = 1 };

        var step2 = new Label {
            X = 0, Y = 5, Width = 70, Height = 2,
            Text = "Once this text field is focused, pasting magically starts to work\neven in the field above."
        };
        var fieldB = new TextField { Width = 70 };
        var frameB = new SimpleFrame(fieldB) { X = 0, Y = 8, Width = 70, Height = 1 };

        Add(step1, frameA, step2, frameB);

        var closeButton = new Button { Text = "Close" };
        closeButton.Accepting += (_, e) => { e.Handled = true; RequestStop(); };
        AddButton(closeButton);
    }
}

internal sealed class MainWindow: Window
{
    public MainWindow()
    {
        Title = " Paste Bug Repro - Main Window ";

        var openButton = new Button { X = 1, Y = 1, Text = "_Open Dialog" };
        openButton.Accepting += (_, e) => { e.Handled = true; App!.Run(new ReproDialog()); };
        Add(openButton);
    }
}
