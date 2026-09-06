## Why

`PublishTab`'s Payload field is a live, always-editable multiline `TextView`, unlike Subject and
Headers. That makes it the only band where keyboard focus gets trapped: Tab types a literal tab
character instead of moving focus, and arrow keys move the text cursor instead of reliably
bubbling out to focus navigation. Since the app is designed to be used entirely without a mouse
(see CLAUDE.md), there is no way to keyboard-navigate out of Payload once it's focused, which
breaks the otherwise-consistent Tab/arrow navigation across the tab's three fields.

## What Changes

- The Payload field gains two focus-scoped states:
  - **Navigate** (default; entered whenever focus lands on Payload via Tab/Shift-Tab): arrow
    keys and Tab/Shift-Tab move focus to Subject, Headers, or Send, exactly as they already do
    for the other bands. The field's text is displayed but not edited.
  - **Edit** (entered with Ctrl+E or Enter while Payload is focused in Navigate; exited with
    Esc): arrow keys and Tab behave exactly as they do today (cursor movement, literal tab
    insertion) — unchanged from current behavior.
- `ShortcutTracker`/`ShortcutAggregator` (already implemented, but per its own comment never
  wired into any status bar) is wired into `MainWindow`'s `StatusBar`, so the
  currently-available shortcuts — Payload's new Ctrl+E/Esc, and Headers' existing Ctrl+N/E/D —
  are visibly discoverable instead of only working if you already know they exist.
- No visual restyling of the Payload frame for Navigate vs. Edit is included in this change —
  that's being designed separately as a general focus-hint treatment.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-publish`: the Payload field's editing requirement changes from "always live for typing"
  to "gated by an explicit Navigate/Edit toggle (Ctrl+E or Enter to start editing, Esc to stop)",
  resolving the keyboard focus trap. Subject and Headers' existing behavior is unchanged.
- `keyboard-shortcut-discovery`: adds a requirement that the currently-available shortcut set
  computed by the existing aggregation/recompute logic is actually rendered somewhere visible
  (the `StatusBar`), not just computed and left unused.

## Impact

- `src/lazynats/PublishTab.cs`: Payload band gains the Navigate/Edit state and its Ctrl+E/Enter/
  Esc key handling.
- `src/lazynats/MainWindow.cs`: `StatusBar` construction changes from a fixed shortcut list to
  one that also reflects `ShortcutTracker.ShortcutsChanged`.
- `src/lazynats/Components/ListEditorView.cs`: no behavior change; its existing
  `IShortcutSource.Shortcuts` (Ctrl+N/E/D) become visible in the `StatusBar` for the first time
  as a side effect of the wiring above.
- Subject field and Header editor: unaffected.
