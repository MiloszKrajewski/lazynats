using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace lazynats.Components;

// Shared "open the shortcut picker for a given start view" sequence, factored out of MainWindow
// and MessageDetailDialog (see openspec/changes/extract-shortcut-picker-key-binding/design.md) -
// both independently hand-rolled the same deferred-collect-run-invoke dance, differing only in
// which view they started ShortcutAggregator.Collect from.
internal static class ShortcutPickerLauncher
{
    // Single definition of the picker's trigger key - callers that also need to display it (e.g.
    // MainWindow's status-bar ShortcutHint) reference this instead of their own literal. Bare `?`,
    // not Ctrl-/, Alt-/, F1: see MainWindow's original comment history in
    // openspec/changes/add-shortcut-picker/design.md for why those were rejected.
    public static readonly Key Key = new('?');

    // Deferred via AddTimeout(Zero, ...): this Action normally runs from inside a still-unwinding
    // KeyDown dispatch, and App.Run() pumps a nested loop that re-observes that same in-flight
    // keypress as unhandled, feeding it back into the caller's binding and potentially
    // re-triggering/stacking. Deferring to the next main-loop iteration runs this after that
    // dispatch has fully unwound, breaking the re-entrancy. startView is a Func, not a snapshot,
    // because it must be evaluated once the deferred callback actually runs - one main-loop
    // iteration after the key was pressed, by which point focus may have changed since the Action
    // was constructed (e.g. MainWindow builds its ShortcutHint list once at startup, but the
    // current focus target changes call to call).
    //
    // owner.App (not a caller-supplied IApplication) for the same "evaluate at invocation time"
    // reason: MainWindow calls this from its own constructor, before it's ever been passed to
    // App.Run<T>() - View.App is still null at that point (confirmed via tmux: reading it eagerly
    // there and capturing the value crashed `?` with NullReferenceException). Reading owner.App
    // here, inside this closure, defers that read until the action actually runs - one main-loop
    // iteration later, by which point owner is part of the running top-level and App is set. No
    // caller-supplied Func<IApplication> needed for this: owner is already a plain reference (not
    // itself lazily evaluated), so deferring the *property read* on it, rather than deferring via
    // an extra Func the caller has to construct, gets the same laziness for free.
    //
    // ShortcutAggregator.Collect only walks upward (View.SuperView) from startView() - it never
    // descends into children - so startView must resolve to (or above) whichever focused leaf
    // actually advertises shortcuts; see each call site for why its own choice is correct.
    public static Action MakeAction(View owner, Func<View?> startView) =>
        () => owner.App!.AddTimeout(
            TimeSpan.Zero, () => {
                var hints = ShortcutAggregator.Collect(startView());
                var dialog = new ShortcutPickerDialog(hints);
                owner.App!.Run(dialog);
                dialog.Result?.Action();
                return false;
            });

    // Convenience wrapper for callers with no dispatch mechanism of their own (unlike MainWindow's
    // topLevelShortcuts loop). Raw KeyDown, not AddCommand/KeyBindings(Command.Context): confirmed
    // via tmux that Command.Context specifically never reaches a custom handler here - Terminal.Gui's
    // built-in "open context/popover menu" semantics for that command appear to take over first.
    //
    // startView defaults to null (auto-resolve via ResolveStartView) rather than requiring every
    // dialog to pick between `() => owner` and `() => owner.MostFocused` itself - that choice used
    // to be hand-reasoned per call site (see this method's git history), which is exactly the kind
    // of caller-side reasoning ResolveStartView now does at runtime instead, from the one thing
    // that actually determines the right answer: whether the focus-chain walk reaches owner at all.
    // Still overridable for a caller that genuinely needs a different policy (there is none today).
    public static void BindKey(View owner, Func<View?>? startView = null)
    {
        var action = MakeAction(owner, startView ?? (() => ResolveStartView(owner)));
        owner.KeyDown += (_, key) => {
            if (key != Key) return;
            key.Handled = true;
            action();
        };
    }

    // Picks the walk's start point from runtime focus topology instead of caller-supplied policy.
    // ShortcutAggregator.Collect only walks upward (View.SuperView) from whatever it's given, so
    // starting from owner.MostFocused only works when that walk actually passes through owner on
    // its way up - true for ordinary content (confirmed via tmux for PublishDialog, both with its
    // Subject field and its header list focused: TextField/ListView -> ... -> PublishDialog).
    // It's false when focus has fallen back onto Terminal.Gui's internal, non-content scaffolding
    // for a dialog with nothing genuinely focusable in it (confirmed via tmux for
    // MessageDetailDialog, whose Labels are all CanFocus=false and whose presentation dropdown
    // starts CanFocus=false too: MostFocused resolves to a bare View living inside the dialog's own
    // Padding adornment, and Adornments (Margin/Border/Padding) aren't linked into the ordinary
    // parent-child SuperView chain the way real content views are - that walk dead-ends at null
    // after 2 hops, never reaching the dialog, even though the dialog itself is a perfectly good
    // IShortcutSource). Falling back to owner itself in that case is always safe: when owner isn't
    // an IShortcutSource, Collect(owner) just finds nothing, same as if the picker had genuinely
    // had nothing to show; when it is (MessageDetailDialog), this is exactly the right answer.
    private static View? ResolveStartView(View owner)
    {
        for (var view = owner.MostFocused; view is not null; view = view.SuperView)
            if (ReferenceEquals(view, owner)) return owner.MostFocused;
        return owner;
    }
}
