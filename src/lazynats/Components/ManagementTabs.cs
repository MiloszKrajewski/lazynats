using Terminal.Gui.ViewBase;

namespace lazynats.Components;

// Thin lazynats-specific subclass of TabbedView: applies this app's theme colors (TabbedView itself
// stays Theme-independent, see Theme.cs's TabAccentColor/TabDimColor/TabSelectedForegroundColor
// comment), keeps MainWindow's call sites (Add, SelectTab) structurally similar to the previous
// Tabs-based implementation, and adds the one piece of Tab/Shift+Tab handling that's genuinely
// lazynats-specific (exiting a FilterBox). Up/Down/Left/Right/Tab-Shift+Tab containment, caption
// click, and everything else is TabbedView's own native behavior (see
// openspec/changes/adopt-tabbed-view/design.md and archived
// openspec/changes/archive/2026-09-10-spike-tab-strip-control).
internal sealed class ManagementTabs: TabbedView
{
    private readonly List<View> _contents = [];

    public ManagementTabs()
    {
        AccentColor = Theme.TabAccentColor;
        DimColor = Theme.TabDimColor;
        SelectedForegroundColor = Theme.TabSelectedForegroundColor;
    }

    // Registers a tab with an explicit caption, replacing Tabs.Add(params View[])'s old
    // per-view-Title convention (TabbedView.AddTab takes the caption directly, not read off the
    // content view's own Title).
    public void Add(string caption, View content)
    {
        _contents.Add(content);
        AddTab(caption, content);
    }

    // Selects `tab` and focuses its content, resolving TabbedView's index-based Select/
    // SelectAndFocusContent model against the content view MainWindow already holds a reference to
    // - keeps MainWindow's call sites (tabs.SelectTab(subscribeTab), etc.) unchanged.
    public void SelectTab(View tab)
    {
        var index = _contents.IndexOf(tab);
        if (index >= 0)
        {
            SelectAndFocusContent(index);
        }
    }

    // Tab pressed while focus is already inside a FilterBox exits it safely back to the list it
    // filters - the only entry point into a FilterBox is "/" (FilterBox.Activate, called from the
    // attached list's own Command.Find binding), never Tab, so this only ever needs to handle the
    // exit direction. Falls back to TabbedView's own generic AdvanceWithinContent for anything else
    // (a tab with no FilterBox, or focus already outside one).
    protected override bool? AdvanceWithinContent()
    {
        for (var view = App?.Navigation?.GetFocused(); view is not null; view = view.SuperView)
        {
            if (view is FilterBox { Target: { } target })
            {
                target.FocusList();
                return true;
            }
        }

        return base.AdvanceWithinContent();
    }
}
