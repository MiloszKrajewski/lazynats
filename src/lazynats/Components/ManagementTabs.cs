using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Components;

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

        // Tab/Shift+Tab resolve differently from ordinary keys in Terminal.Gui: rather than
        // bubbling through each ancestor's own KeyBindings from the focused view outward (how
        // Ctrl+R, Up/Down above, etc. all work), they route straight to the nearest enclosing
        // TabGroup - which, for every management tab's content, is this view (Tabs sets
        // TabStop = TabBehavior.TabGroup on itself). A FilterBox or DrillableListView<T> binding
        // Key.Tab on themselves is never actually consulted; this is the only place in the tree
        // such a binding can take effect at all. Left to Terminal.Gui's own default AdvanceFocus,
        // reaching either end of a page's own focus chain (now that a page can have more than one
        // focusable child) escapes to the tab's own header, or even a *different* management tab
        // entirely - the same kind of leak the class comment above describes for arrow keys, just
        // via Tab instead. See AdvanceWithinPage.
        //
        // Deliberately bound to Command.Accept, not Command.NextTabStop/PreviousTabStop - the
        // list<->FilterBox toggle AdvanceWithinPage implements is direction-agnostic (there are
        // only ever the two of them), but more importantly, routing *through* NextTabStop/
        // PreviousTabStop specifically - even with a custom handler overriding them here - left
        // Terminal.Gui's own internal Tab-navigation bookkeeping in a state where, once
        // PreviousTabStop (Shift+Tab) had fired once in a session, NextTabStop (Tab) silently
        // stopped reaching this view's KeyBindings at all for the rest of the session (reproduced
        // repeatedly; Shift+Tab kept working indefinitely, only Tab broke, and only after the
        // first Shift+Tab). Using an unrelated Command sidesteps whatever that internal coupling
        // is entirely.
        KeyBindings.Add(Key.Tab, Command.Accept);
        KeyBindings.Add(Key.Tab.WithShift, Command.Accept);
        AddCommand(Command.Accept, AdvanceWithinPage);
    }

    // Switches to `tab` and focuses its default target (skipping any FilterBox - see
    // FindFirstFocusableDescendant), rather than whatever the base Value setter's own SetFocus()
    // picks by default (its own first-in-SubViews-order descendant, unaware of FilterBox's spatial
    // Add()-order-driven placement ahead of the list it filters). Every direct-selection path
    // (Alt+1..4 shortcuts, the initial tab on startup) goes through this, not `Value = tab`
    // directly - unlike arrow-key header<->content navigation (FocusOwnContent), which already
    // resolves the same way.
    public void SelectTab(View tab)
    {
        Value = tab;
        (FindFirstFocusableDescendant(tab) ?? tab).SetFocus();
    }

    // A list<->FilterBox pairing toggles directly between the two, regardless of direction (there
    // are only ever the two of them) - walks up from the actually-focused view (not just Value's
    // immediate child) looking for either half of such a pairing and focuses it. Falls back to
    // generic forward AdvanceFocus for anything else (a tab with no FilterBox, or focus already
    // outside any pairing) - unchanged from, and no worse than, Terminal.Gui's own default there.
    private bool? AdvanceWithinPage()
    {
        for (var view = App?.Navigation?.GetFocused(); view is not null; view = view.SuperView)
        {
            if (view is FilterBox { Target: { } target })
            {
                target.FocusList();
                return true;
            }

            if (view is IFilterable { AttachedFilterBox: { } box })
            {
                box.Focus();
                return true;
            }
        }

        App?.Navigation?.AdvanceFocus(NavigationDirection.Forward, TabBehavior.TabStop);
        return true;
    }

    private bool? FocusOwnHeader()
    {
        if (Value?.Border.View is { HasFocus: false } headerView) 
            (headerView as BorderView)?.TitleView?.SetFocus();
        return true;
    }

    private bool? FocusOwnContent()
    {
        // RestoreFocus() (which the base class's FocusContent() uses to return to whatever was
        // previously focused within the content) is internal, not accessible from this assembly;
        // SetFocus() - the same public method the base Value setter itself uses - is the
        // sanctioned substitute, at the cost of always landing on the content's default focus
        // target rather than wherever focus was before the header was entered. That "default
        // target" can resolve to Value itself when Value is CanFocus while also hosting its own
        // focusable descendants (every tab's content today) - an invisible, non-interactive focus
        // target indistinguishable from focus having gone nowhere. Finding and focusing the first
        // focusable descendant explicitly removes that ambiguity.
        if (Value?.Border.View is not { HasFocus: true } headerView) 
            return true;

        headerView.HasFocus = false;
        var target = Value is { } content ? FindFirstFocusableDescendant(content) ?? content : null;
        target?.SetFocus();

        return true;
    }

    private static View? FindFirstFocusableDescendant(View view)
    {
        foreach (var sub in view.SubViews)
        {
            // A FilterBox is deliberately Add()-ed before its list (so Tab/Shift+Tab cycles in
            // top-down spatial order - see each *Tab.cs's Add() call) but should never itself be
            // where a tab's content lands by default on entry: skipped here entirely (not just
            // deprioritized) so this DFS's "first" is still the list, independent of that order.
            if (!sub.Visible || !sub.Enabled || sub is FilterBox) continue;

            var deeper = FindFirstFocusableDescendant(sub);
            if (deeper is not null) return deeper;
            if (sub.CanFocus) return sub;
        }

        return null;
    }

    private bool? SwitchTab(int direction)
    {
        if (Value?.Border.View?.HasFocus != true) 
            return true;

        var tabs = TabCollection.ToList();
        var index = tabs.IndexOf(Value);
        if (index < 0) 
            return true;

        Value = tabs[(index + direction + tabs.Count) % tabs.Count];
        (Value?.Border.View as BorderView)?.TitleView?.SetFocus();

        return true;
    }
}
