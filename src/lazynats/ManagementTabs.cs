using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats;

// Tabs' own Command.Up/Down/Left/Right handlers switch tabs from any arrow key left unhandled by
// a tab's content, not just ones that started on a genuinely-focused header. This scopes
// Left/Right to header-focus only, makes Up climb to the *current* tab's own header instead of the
// base class's "jump to the previous tab" default, and makes Down return focus from a focused
// header back into that tab's content.
//
// SelectNextTab()/SelectPreviousTab()/GetTabs() on the base class are private, so tab cycling is
// reimplemented here against the public TabCollection/Value API rather than delegating to them.
//
// All four overrides unconditionally report the key as handled (`return true`), even in their
// "nothing to do" branches - including Down, whose base FocusContent() already declines correctly
// when content (not a header) has focus. Discovered by manually driving the app: reporting a key
// as unhandled here doesn't make it an inert no-op - it lets Terminal.Gui's key routing keep
// walking past this view to some further ancestor's own generic arrow-key focus-navigation, which
// can land on a *different* tab's content and silently retrigger Value via Tabs.OnFocusedChanged
// (e.g. pressing Down at the bottom of a single-item list). That's the exact arrow-key tab-switch
// leak this class exists to close, just reopened via the decline path. Absorbing the key here,
// always, is what actually keeps it from happening.
internal sealed class ManagementTabs: Tabs
{
    public ManagementTabs()
    {
        AddCommand(Command.Up, FocusOwnHeader);
        AddCommand(Command.Down, FocusOwnContent);
        AddCommand(Command.Left, () => SwitchTab(-1));
        AddCommand(Command.Right, () => SwitchTab(1));
    }

    private bool? FocusOwnHeader()
    {
        if (Value?.Border.View is { HasFocus: false } headerView) (headerView as BorderView)?.TitleView?.SetFocus();
        return true;
    }

    private bool? FocusOwnContent()
    {
        // RestoreFocus() (which the base class's FocusContent() uses to return to whatever was
        // previously focused within the content) is internal, not accessible from this assembly;
        // SetFocus() - the same public method the base Value setter itself uses - is the
        // sanctioned substitute, at the cost of always landing on the content's default focus
        // target rather than wherever focus was before the header was entered.
        if (Value?.Border.View is { HasFocus: true } headerView) {
            headerView.HasFocus = false;
            Value?.SetFocus();
        }

        return true;
    }

    private bool? SwitchTab(int direction)
    {
        if (Value?.Border.View?.HasFocus == true) {
            var tabs = TabCollection.ToList();
            var index = tabs.IndexOf(Value);
            if (index >= 0) {
                Value = tabs[(index + direction + tabs.Count) % tabs.Count];
                (Value?.Border.View as BorderView)?.TitleView?.SetFocus();
            }
        }

        return true;
    }
}
