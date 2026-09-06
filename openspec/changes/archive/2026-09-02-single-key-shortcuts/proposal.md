## Why

Ctrl-based list shortcuts (Ctrl+N/E/D/R/F/S/X/O) collide with terminal-level control codes on some
setups (Ctrl+S/Ctrl+Q are XON/XOFF flow control in many terminal stacks, Ctrl+H/Ctrl+M/Ctrl+I alias
Backspace/Enter/Tab) and never reach the app at all. Every tab-hosted list already disables its
`ListView`'s own type-to-jump navigation and has no persistent free-typing field except the
quick-search box, so bare letter keys are free to use instead — matching the precedent
`LiveUpdatesView` already ships (`C` for Clear). Making that swap safe also requires tightening how
the quick-search field claims and releases keyboard focus, since a bare `N`/`E`/`D`/... typed while
that field is focused must be plain text input, never a shortcut.

## What Changes

- Tab-hosted list operations move from Ctrl+N/E/D/R/F (and the Objects/Templates-specific Ctrl+S,
  Ctrl+X, Ctrl+O) to bare N/E/D/R/F/S/X/O. **BREAKING**: existing muscle memory for these Ctrl
  combinations stops working on every tab-hosted list.
- Quick-search (`/`) becomes the *only* way to focus the search field — removes the Up-arrow-at-
  list-top shortcut and the Tab/Shift+Tab list⇄search toggle as entry paths. `FilterBox.CanFocus`
  is `false` except while active, closing off mouse-click and generic tab-order entry too.
- Leaving the search field (Enter, Esc, Tab/Shift+Tab, an arrow key, or a mouse click elsewhere) always
  returns focus to the list and makes the field unfocusable again, via one generic focus-lost hook
  rather than bespoke handling per exit key.
- Esc semantics on quick-search change from "clear the field" to "cancel/undo": the field
  snapshots its text on activation, and Esc restores that snapshot (reverting any narrowing typed
  during the session) instead of always blanking it. Esc on an already-empty field still ascends a
  level where that's supported; where it isn't, it now falls back to plain cancel/defocus instead
  of leaving focus stranded in the field — fixing a pre-existing bug (see design.md).
- Enter/Tab/arrow/click-away need no new "apply" step: quick-search already narrows the list live
  on every keystroke, so whatever's currently typed is already in effect when focus leaves.
- Standalone/modal list-editor usage (`HeaderEditorView` inside `PublishDialog`) is explicitly out
  of scope and keeps Ctrl+N/E/D/F — it shares a modal with genuine free-text fields (Subject,
  Payload) that bare letters would collide with.
- The global Shortcuts-picker trigger moves from Alt+K to `?`, matching the same top-level pattern
  as `/` for quick-search: reachable from anywhere in the app, safe against colliding with typed
  text because Terminal.Gui always offers a focused text field first refusal at a key before any
  ancestor (here, `MainWindow`) ever sees it — the same mechanism that already makes the bare-letter
  list shortcuts safe.

## Capabilities

### New Capabilities
(none — this reshapes existing shortcut and quick-search behavior, it doesn't introduce a new one)

### Modified Capabilities
- `tab-scoped-list-shortcuts`: the keys a tab dispatches (Ctrl+N/D/R/E/F) become bare N/D/R/E/F.
- `drillable-list`: Shared Create/Delete/Edit/Filter Wiring's key references change to bare
  letters; Shared Quick-Search Wiring is rewritten for `/`-only entry, generic focus-loss exit, and
  snapshot/revert Esc semantics.
- `list-editor`: Ctrl+N/E/D/F key references change to bare letters for tab-hosted usage; standalone
  usage (bindSharedKeys=true) is called out as unaffected.
- `list-filter-affordance`: Ctrl+F references become bare F.
- `tab-navigation`: the Up-climb-to-header scenario's description of climbing through an
  intermediate "text input" (the search field) stop is updated, since that field is no longer an
  Up-arrow stop on the way to the tab header.
- `nats-streams`, `nats-kv`, `nats-obj`, `nats-templates`, `nats-subscriptions`: each spec restates
  the shared shortcuts' keys directly in its own scenarios (Ctrl+N/E/D/R/F, plus Ctrl+S in
  `nats-obj` and Ctrl+X/Ctrl+O in `nats-templates`); all get the same bare-letter rename for
  consistency with the capabilities above.
- `shortcut-picker`: its trigger key changes from Alt+K to `?` throughout (the invocation
  requirement, and every mention of the trigger key among the excluded top-level shortcuts).

## Impact

- `src/lazynats/Components/ListEditorView.cs`, `DrillableListView.cs`, `FilterBox.cs`,
  `ManagementTabs.cs`
- `src/lazynats/Objects/ObjectListView.cs`, `src/lazynats/Templates/TemplatesTab.cs` (bespoke
  Ctrl+S/X/O bindings)
- `src/lazynats/MainWindow.cs` (`topLevelShortcuts`' Alt+K entry and its rationale comment)
- Every management tab that hosts a `DrillableListView<T>`/`ListEditorView<T>` with an attached
  `FilterBox`: `StreamsTab`, `ValuesTab`, `ObjectsTab`, `TemplatesTab`, `SubscribeTab` (the last has
  no `FilterBox`, so only the bare-letter rename applies there)
- Not touched: `PublishDialog`/`HeaderEditorView` (standalone list-editor usage stays Ctrl-based),
  every field-entry modal dialog (`CreateStreamDialog`, `PatternDialog`, etc. — separate `Toplevel`s,
  unaffected by either change)
