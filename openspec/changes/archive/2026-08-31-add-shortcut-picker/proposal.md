## Why

The status bar's dynamic shortcut tail (`openspec/specs/keyboard-shortcut-discovery/spec.md`)
grows with whatever the focused view advertises, and there isn't enough horizontal room for it
once a view advertises more than a couple of shortcuts — labels get cut or the bar wraps. We
still want every advertised shortcut to be discoverable, just not by permanently occupying
status-bar real estate.

## What Changes

- The status bar's shortcut row becomes a **fixed, hardcoded** set of top-level/global shortcuts
  only (`Alt-1..4` tab switches, `Alt-P` publish, `Alt-Q` quit, `Alt-K` shortcut picker).
  **BREAKING**: the status bar no longer displays per-view dynamic shortcuts
  (`IShortcutSource`-advertised hints) directly — this is a behavior change to an existing,
  archived requirement ("Aggregated Shortcuts Are Rendered in the Status Bar").
- A new global shortcut opens a modal shortcut picker listing the focused view's advertised
  shortcuts — the same focus-chain aggregation `ShortcutAggregator`/`ShortcutTracker` already
  compute, now consumed on demand instead of continuously pushed to the status bar. It
  deliberately excludes the hardcoded top-level set (see below) — those are already permanently
  visible in the status bar, so listing them again would be redundant. A view advertising no
  shortcuts of its own shows an empty-state message instead of a blank list.
  - **Trigger key iterated during implementation**: `Ctrl+/` (the original choice) does nothing on
    Windows — confirmed both in Terminal.Gui's own docs (their built-in Undo binding is
    `Bind.AllPlus("Ctrl+Z", nonWindows: ["Ctrl+/"])`, excluding Windows entirely) and by the user
    directly on their real terminal. `Alt+/` fared no better. `F1` worked but function keys are
    unreliable on some laptop keyboards. Settled on **`Alt+K`**, matching the rest of the
    top-level set's `Alt+<key>` shape and confirmed working.
- The picker lists entries alphabetically by name (not by key chord), since names are no longer
  constrained by status-bar width and can be more descriptive.
- Selecting an entry and pressing Enter closes the picker first, then invokes that shortcut's
  action (closed-then-invoke, not invoke-then-close). Esc closes the picker with no action.
- While the picker is open, the underlying view's own ordinary key bindings do not fire — this
  falls out of the picker being an ordinary modal `Dialog`, matching how `PatternDialog` /
  `CreateStreamDialog` / etc. already suppress background key handling; no new key-routing
  mechanism is needed for that part.
- **BREAKING (found and fixed during implementation, beyond the picker itself)**: the top-level
  shortcuts (`Alt-1..4`, `Alt-P`, `Alt-Q`) no longer interrupt an already-open dialog anywhere in
  the app. They previously used `BindKeyToApplication = true`, which live testing showed bypasses
  modal key-routing entirely — letting them (and the picker's own trigger key) fire while a dialog,
  including the picker itself, was already open, up to the picker re-triggering and stacking on
  itself. Removed project-wide in favor of an ordinary `MainWindow.KeyDown` handler, at the user's
  request (see design.md).
- Out of scope for this change: shortcut discovery while another modal dialog is already the
  active toplevel (this remains true — none of the top-level set fires while any dialog is open,
  the picker included), `/`-triggered fuzzy filtering inside the picker, and shortcut grouping/
  categorization (noted as future work, not designed here).

## Capabilities

### New Capabilities
- `shortcut-picker`: the `Alt+K` modal dialog that lists the focused view's focus-chain-advertised
  shortcuts (not the hardcoded top-level set — see above) alphabetically by name, runs the
  selected shortcut's action on Enter after closing, and does nothing on Esc.

### Modified Capabilities
- `keyboard-shortcut-discovery`: the requirement "Aggregated Shortcuts Are Rendered in the
  Status Bar" is replaced — the status bar shows only a fixed, hardcoded top-level shortcut set,
  and the focus-chain aggregation (`ShortcutAggregator`) is instead consumed on demand by the new
  shortcut picker rather than continuously synced to status-bar widgets.

## Impact

- `src/lazynats/MainWindow.cs`: status bar construction drops the dynamic-tail wiring
  (`ShortcutTracker.ShortcutsChanged` → `SyncDynamicShortcuts`) and gains an `Alt-K` entry that
  opens the picker; the fixed top-level shortcuts stay hardcoded here as today, but now trigger via
  a `KeyDown` handler on `MainWindow` instead of `BindKeyToApplication` (see the BREAKING note
  above) — the status-bar `Shortcut` widgets keep their `Key`/`Text`/`Action` for display and
  mouse-click support only.
- `src/lazynats/Components/ShortcutAggregator.cs`: `ShortcutTracker`'s continuous
  `FocusedChanged`-driven push model is no longer needed by the status bar; it becomes an
  on-demand snapshot call (`ShortcutAggregator.Collect(...)`) made when the picker opens. Whether
  `ShortcutTracker` itself is simplified/removed or kept as a thin wrapper is a design-time
  decision.
- New component (naming TBD in design): a `Dialog<ShortcutHint>` subclass rendering the
  alphabetical list.
- No change to per-view `IShortcutSource` implementations' contract, though their advertised
  `Text` labels may be revised to be more descriptive now that status-bar width no longer
  constrains them (left to each view, not required by this change).
