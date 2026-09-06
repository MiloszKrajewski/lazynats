using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

internal sealed class MainWindow: Runnable
{
    public MainWindow()
    {
        var heartbeat = Services.Root.GetRequiredKeyedService<IObservable<string>>("heartbeat");
        var liveUpdates = new LiveUpdatesView(heartbeat) {
            X = 0, Y = Pos.AnchorEnd(),
            Width = Dim.Fill(), Height = Dim.Percent(25),
        };
        liveUpdates.ItemSelected += text => MessageBox.Query(App!, "Selected", text, "_Ok");

        // Dim.Fill(1) leaves the bottom row free for the StatusBar, which sits outside this frame.
        var frame = new FrameView { Title = " My App ", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1) };
        frame.Add(liveUpdates);

        var quitShortcut = new Shortcut { Text = "Quit", Key = Key.Q.WithAlt, BindKeyToApplication = true };
        quitShortcut.Action = () => App!.RequestStop();

        var clearShortcut = new Shortcut { Text = "Clear", Key = Key.C, Visible = false };
        clearShortcut.Action = () => liveUpdates.Clear();
        liveUpdates.HasFocusChanged += (_, _) => clearShortcut.Visible = liveUpdates.HasFocus;

        var statusBar = new StatusBar([quitShortcut, clearShortcut]);

        Add(frame, statusBar);
    }
}