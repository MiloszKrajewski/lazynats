## Context

`MainWindow` is a `Runnable`. Terminal.Gui v2 binds the quit key (Esc by default) to
`Command.Quit` at the Application level (`ApplicationKeyboard`), and an Esc that nothing deeper
in the focus chain handles ends up stopping the top runnable, i.e. quitting the app. Deeper views
already claim Esc as "back" when it means something to them: `DrillableListView` binds Esc to
`Command.Cancel` (ascend) only while ascend is enabled, `FilterBox` clears on Esc, and every
modal dialog closes on Esc. Key dispatch is depth-first (the focused view gets first refusal -
the same property the bare `?` shortcut in `MainWindow.cs` relies on), so an Esc that reaches
`MainWindow` is by definition "nothing left to go back from".

The explicit quit path is separate: the Alt+Q `ShortcutHint` in `MainWindow`'s `globalShortcuts`
calls `App!.RequestStop()` directly.

## Goals / Non-Goals

**Goals:**
- A top-level Esc does not quit the app.
- Zero change to Esc behavior anywhere below the top level.
- Alt+Q stays a one-press quit.

**Non-Goals:**
- A confirm-before-quit dialog (considered, see Decision 2).
- Confirming other exit paths (Alt+Q, terminal close, Ctrl+C / process signals).
- Making the quit key configurable, or advertising Esc in the status bar.

## Decisions

### 1. Swallow the quit key in `MainWindow`'s `KeyDown`

Verified by decompiling Terminal.Gui 2.4.17's `ApplicationKeyboard.RaiseKeyDownEvent`: the key
goes to `TopRunnableView.NewKeyDownEvent` first, and the Application-level `Command.Quit` binding
only runs if that returns unhandled. `MainWindow` has no `Command.Quit` binding of its own, so
re-registering the command via `AddCommand` (the originally preferred option) would never be
reached. Instead, `MainWindow`'s existing `KeyDown` loop marks the key handled when it matches
`Application.GetDefaultKeys(Command.Quit)` - tied to the quit command's binding, so it follows the
quit key if it is ever rebound, rather than hardcoding `Key.Esc`.

- Alt+Q is unaffected: it calls `RequestStop()` directly and never goes through `Command.Quit`.
- Alternative: override `OnIsRunningChanging` and cancel the stop. Rejected - it catches every
  stop path (Alt+Q included) and would need a bypass flag.

### 2. No-op rather than a confirmation dialog

A `MessageBox.Query` confirmation was implemented first, but `MessageBox` can't take the
project's `Padding.Thickness` / bordered-container spacing conventions, and a dedicated
`Dialog` subclass just for this was judged not worth it. Swallowing the key removes the
accidental-quit hazard just as well; Alt+Q, advertised in the status bar, is the quit path.

## Risks / Trade-offs

- [Users who liked Esc-to-quit lose it] → Alt+Q remains a one-press quit and is advertised in
  the status bar.
- [A view that currently lets Esc fall through by accident now does nothing instead of quitting]
  → that is exactly the intended safety net.
