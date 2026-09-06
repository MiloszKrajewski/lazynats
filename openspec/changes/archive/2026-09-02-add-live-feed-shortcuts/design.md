## Context

`MainWindow` builds `topLevelShortcuts` (a `List<ShortcutHint>`) for `Alt+1..5`/`Alt+P`/`Alt+Q`,
each entry driving both a `KeyDown` dispatch and a permanent status-bar `Shortcut` widget.
Separately, `ShortcutAggregator.Collect(focused)` walks the focused view's ancestor chain
collecting `IShortcutSource.Shortcuts`, used only by the `?` picker (`ShortcutPickerDialog`) —
per `openspec/changes/archive/2026-08-31-add-shortcut-picker/design.md`, that picker deliberately
excludes `topLevelShortcuts` since those are already permanently visible.

`LiveUpdatesView`'s `Clear` binding (`KeyBindings.Add(Key.C, Command.DeleteAll)`) predates both of
these: it was added before the `IShortcutSource` convention existed, so `MainWindow` wires its
status-bar visibility by hand via `HasFocusChanged`, and it was never migrated when other views
adopted the discoverable pattern. `ManagementTabs.SelectTab` already does `Value = tab;
(FindFirstFocusableDescendant(tab) ?? tab).SetFocus()`, which is why `Alt+1..5` reads as "jump to
this pane," not just "switch tab" — `LiveUpdatesView` has no equivalent entry point.

## Goals / Non-Goals

**Goals:**
- `Alt+M` moves keyboard focus into `LiveUpdatesView` from anywhere, matching the jump-and-focus
  behavior `Alt+1..5` already provide for management tabs.
- `Clear` is discoverable via the `?` picker like every other view's shortcuts, with no
  special-cased status-bar widget.

**Non-Goals:**
- Changing `ShortcutAggregator`, `ShortcutPickerDialog`, or the top-level `KeyDown` dispatch
  mechanism itself.
- Adding a way to jump focus *back* from the feed to the tabs beyond what already exists
  (`Alt+1..5`, `Tab`/`Shift+Tab`).
- Any change to feed behavior (dedup, batching, rendering) — this is UI wiring only.

## Decisions

**`Alt+M`, not `Alt+L`.** Both were free of mnemonic collisions (no `_L`/`_M` button mnemonic
exists anywhere in the app, and dialog-local mnemonics never overlap `MainWindow`'s
`topLevelShortcuts` regardless — a modal dialog running means `MainWindow` is not part of the key
dispatch chain). Confirmed with the user: `M` for "Message(s)", matching `doc/UI.md`'s framing of
feed entries as messages.

**Drop the dedicated `Clear` status-bar widget rather than keep it alongside `?` discovery.**
Every other `IShortcutSource` (`DrillableListView`'s `Back`/`Search`, `ListEditorView`'s
`New`/`Edit`/`Delete`) is `?`-only; none get a permanent status-bar entry. Keeping `Clear`'s
widget in addition to making it `?`-discoverable would leave Live Feed as the one inconsistent
exception for no behavioral reason. Confirmed with the user, who chose consistency over the
extra always-visible affordance.

**Focus-jump implemented as a fifth `topLevelShortcuts` entry, not a new mechanism.** `Alt+M` is
exactly the same shape as `Alt+1..5`: a `ShortcutHint` whose action moves focus. No new
abstraction needed — `liveUpdates.SetFocus()` is the direct equivalent of
`ManagementTabs.SelectTab`'s `SetFocus()` call, since `LiveUpdatesView` itself is the focusable
target (its `ListView` child is the first/only focusable descendant).

## Risks / Trade-offs

- [Removing the `HasFocusChanged`-driven widget changes what's visible at a glance while Live Feed
  is focused — a user relying on seeing "Clear" permanently in the status bar loses that until
  they press `?`] → Accepted: this matches how every other view's shortcuts already work, and `?`
  is the app's established discovery path.
