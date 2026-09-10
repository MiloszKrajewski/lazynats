using lazynats.Spike.Tabs;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

Application.Create().Run<MainWindow>().Dispose();

internal sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "lazynats Spike: Tabbed View (Esc to quit)";

        // Mirrors the real app's shape (MainWindow.cs): tabs over a Live Feed frame, so this
        // spike has a genuine second, unrelated focusable region to move focus away from the
        // tabbed view with - its own Tab/Shift+Tab deliberately never leaves it (confirmed live,
        // see FINDINGS.md), matching ManagementTabs, so a real app always needs an out-of-band
        // jump like this Alt+0 to reach anything else.
        _tabbedView = new TabbedView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Percent(75) };
        _tabbedView.AddTab(" _Subscribe ", PlaceholderPage("Subscribe", "This is the Subscribe placeholder page."));
        _tabbedView.AddTab(" S_treams ", PlaceholderPage("Streams", "This is the Streams placeholder page."));
        _tabbedView.AddTab(" _Values ", PlaceholderPage("Values", "This is the Values placeholder page."));
        _tabbedView.AddTab(" _Objects ", PlaceholderPage("Objects", "This is the Objects placeholder page."));
        _tabbedView.AddTab(" _Publish ", PlaceholderPage("Publish", "This is the Publish placeholder page."));
        Add(_tabbedView);

        var liveFeedList = new ListView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true,
            Source = new ListWrapper<string>(["message 1", "message 2", "message 3"]),
        };
        _feedFrame = new FrameView
        {
            Title = " Live Feed ", X = 0, Y = Pos.Bottom(_tabbedView), Width = Dim.Fill(), Height = Dim.Fill(2),
        };
        _feedFrame.Add(liveFeedList);
        Add(_feedFrame);
        _feedBorderView = _feedFrame.Border!.GetOrCreateView();

        KeyBindings.Add(Key.D0.WithAlt, Command.Accept);
        AddCommand(Command.Accept, () => { liveFeedList.SetFocus(); return true; });

        // Diagnostic only, for driving this spike via tmux - shows what currently has keyboard
        // focus, since color/attribute alone is hard to eyeball reliably over tmux.
        _diag = new Label { X = 0, Y = Pos.Bottom(_feedFrame), Width = Dim.Fill(), Height = 2 };
        Add(_diag);
    }

    private readonly TabbedView _tabbedView;
    private readonly FrameView _feedFrame;
    private readonly View _feedBorderView;
    private readonly Label _diag;
    private bool _subscribedFocusedChanged;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var stop = base.OnDrawingContent(context);

        if (!_subscribedFocusedChanged && App is not null)
        {
            App.Navigation!.FocusedChanged += (_, _) => UpdateFeedFrameAccent();

            App.AddTimeout(TimeSpan.FromMilliseconds(300), () => {
                var focused = App!.Navigation?.GetFocused();
                _diag.Text = $"focused={focused?.GetType().Name}:{focused?.Text} selected={_tabbedView.SelectedIndex}";
                _diag.SetNeedsDraw();
                return true;
            });
            _subscribedFocusedChanged = true;
        }

        return stop;
    }

    // Out-of-scope-but-illustrative: gives the Live Feed frame the same yellow-when-focused accent
    // as TabbedView, just to get a feel for what the real MainWindow would look like with both
    // regions using this treatment. Same approach as TabbedView.UpdateCaptionAttributes: scheme goes
    // on Border.GetOrCreateView(), never on _feedFrame itself, so it doesn't bleed into
    // liveFeedList's content; background comes from GetAttributeForRole rather than a hardcoded
    // color so it stays transparent.
    private void UpdateFeedFrameAccent()
    {
        var insideFeed = false;
        for (var v = App?.Navigation?.GetFocused(); v is not null; v = v.SuperView)
        {
            if (v == _feedFrame)
            {
                insideFeed = true;
                break;
            }
        }

        var ambientBackground = _feedFrame.GetAttributeForRole(VisualRole.Normal).Background;
        _feedBorderView.SetScheme(insideFeed ? new Scheme(new Attribute(Color.BrightYellow, ambientBackground)) : null);
    }

    private static View PlaceholderPage(string title, string body)
    {
        var page = new View { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        page.Add(new Label { X = 1, Y = 1, Text = $"{title}\n\n{body}" });
        page.Add(new Button { X = 1, Y = 5, Text = $"_{title} button" });
        return page;
    }
}
