## Context

`MainWindow` builds one `List<ShortcutHint> topLevelShortcuts` (`MainWindow.cs:117-171`). That
single list feeds two things:

1. **Key handling**: `MainWindow`'s own `KeyDown` loop (`:189-197`) matches pressed keys against
   the list and runs the matching action. Only this path actually handles keys.
2. **Status bar display**: `topLevelWidgets` (`:182-187`) turns each entry into a `Shortcut`
   widget, used only for display and mouse clicks.

Today the two views are the same list, so every bound key has its own status bar hint. This
change pulls them apart: six keys stay bound but lose their hints, and one hint (`Alt-#`) has no
key behind it.

The `?` picker (`ShortcutPickerDialog`) never reads `topLevelShortcuts`. It only collects from
`IShortcutSource` views on the focus chain. The comment at `MainWindow.cs:113-116` that claims
otherwise is out of date and gets fixed as part of this change.

## Goals / Non-Goals

**Goals:**
- One display-only `Alt-# Jump` hint as the first status bar entry.
- `Alt-1..5` and `Alt-0` stay bound, with no status bar hints of their own.
- About moves from `F10` to `Alt-A`, and its hint stays in the status bar.

**Non-Goals:**
- Changing the `?` picker's contents or its exclusion of top-level shortcuts.
- Making status bar hints responsive to terminal width (e.g. dropping hints dynamically). This
  change is a static trim.
- Rebinding any other shortcut.

## Decisions

### Split the list by role, not by flag

Split `topLevelShortcuts` into a `jumpShortcuts` list (Alt-1..5, Alt-0) and the remaining
global shortcuts (Quit, Publish, About, `?`):

```
bound keys   = jumpShortcuts + globalShortcuts        -> KeyDown loop
status bar   = [Jump display widget] + globalShortcuts -> widgets
```

Considered and rejected: adding a `ShowInStatusBar`/`DisplayOnly` flag to `ShortcutHint`.
`ShortcutHint` is the shared `IShortcutSource` contract used by every tab. A status bar
visibility flag would only mean something for `MainWindow`'s hardcoded list, and every other
source would carry it unused. Two lists named for their role describe the intent directly. They
are also still one source of truth, since each binding still appears exactly once.

### The Jump hint is a plain `Shortcut` widget with no `Action`

It's built directly as a `Shortcut` widget instead of from a `ShortcutHint`, since it has no key
to handle or action to run. Its key text reads `Alt-#`, drawn in the same key style as its
neighbours.

The open question is how to get that key text. See below.

### About: `Alt-A` via the same deferred-`AddTimeout` action

Only the `Key` changes (`Key.F10` becomes `Key.A.WithAlt`). The deferred-run workaround for
re-entrancy and its comment stay as they are (the comment's mention of "F10" gets updated).
`Alt-A` is free in `MainWindow`'s key chain: tab captions have no `_` mnemonics, and the
`_A...`-style button mnemonics all live inside modal dialogs, which never reach `MainWindow`'s
`KeyDown`.

Resulting status bar order:

```
 Alt-# Jump │ Alt-Q Quit │ Alt-P Publish │ Alt-A About │ ? Shortcuts │ <tab status...>
```

## Risks / Trade-offs

- [Terminal.Gui `Shortcut` registers its own `Key` binding] If the Jump widget is given a real
  `Key` (e.g. `new Key('#').WithAlt`), `Shortcut` may bind it as a hotkey. With no `Action` that
  does nothing, but the key could still be swallowed. → Prefer a display approach that doesn't
  register a binding (see Open Questions). If a `Key` is unavoidable, check with tmux that
  `Alt+#` does nothing visible and that typing `#` in text fields still works.
- [Discoverability of individual digits] Only window titles show which digit goes where. → This
  is intended (the `N:` prefix is the tab-navigation spec's own "Tab Titles" requirement), and
  `Alt-#` points users to it.
- [Muscle memory for `F10`] Existing users pressing `F10` get nothing. → The `Alt-A About` hint
  stays visible, so the new key is easy to find. The app is pre-1.0, so there's no compatibility
  promise to keep.

## Open Questions

- How exactly to render `Alt-#` in the Jump widget's key column: with a real `Key` value (and
  its possible binding side effect), or by setting the key text directly without registering a
  binding. Resolve against Terminal.Gui v2's `Shortcut` API (context7 first, per CLAUDE.md) at
  implementation time. The spec only requires that it looks like the other hints and does
  nothing when pressed.

  **Resolved:** set `KeyView.Text` directly and leave `Key` empty. Checked against the
  decompiled Terminal.Gui 2.4.17 `Shortcut`: `UpdateKeyBindings` only binds a non-empty `Key`,
  and `ShowHide()` (re-run from `OnSubViewLayout`) keeps `KeyView` visible when either
  `Key` is non-empty or `KeyView.Text` is non-empty. So the widget draws a key column but
  registers no binding at all. The text is built as `$"Alt{Key.Separator}#"`, so it uses the
  same separator that `Key.ToString()` gives its neighbours.
