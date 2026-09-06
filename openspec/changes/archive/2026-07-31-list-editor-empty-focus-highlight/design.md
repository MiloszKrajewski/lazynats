## Context

`ListEditorView<T>` (`src/lazynats/Components/ListEditorView.cs`) overlays a dim, non-interactive
`_emptyHintLabel` on top of the `ListView` while the item collection is empty (`list-editor` spec,
"Empty-State Hint"). The label always uses `GetScheme().Disabled` (or a background-adjusted
variant of it via the `Background` setter) regardless of focus, so an empty, focused list editor
is visually indistinguishable from an empty, unfocused one.

A normal (non-empty) `ListView` already shows a focus highlight on its selected row via
Terminal.Gui's own focus-driven role rendering (`VisualRole.Focus` vs `VisualRole.Normal`). This
change gives the empty-state hint the equivalent cue: swap its scheme between a focused and
unfocused variant as the list editor's focus state changes, instead of a fixed `Disabled` scheme.

## Goals / Non-Goals

**Goals:**
- The hint label's *style* reflects whether `ListEditorView<T>` currently holds keyboard focus.
- Preserve the current unfocused appearance (dim, `Disabled`-role) exactly as-is.
- Keep the hint non-interactive and outside list selection/navigation — only the scheme changes.
- Keep working correctly with the existing `Background` override (used by `EditFrame` callers).

**Non-Goals:**
- No change to hint text, visibility timing (still tied purely to `_items.Count == 0`), or
  Ctrl+N/E/D behavior.
- No change to focus highlighting for the non-empty case — `ListView`'s own row highlight is
  untouched.

## Decisions

- **Track focus via `HasFocus` change, not a new field driving visibility logic.** Subscribe to
  the base `View`'s focus-changed mechanism (Terminal.Gui v2 raises focus events on the view; see
  `doc/terminal-gui-howto.md` for the current API) at the `ListEditorView<T>` level — the same
  level the Ctrl+N/E/D bindings already live at — and recompute `_emptyHintLabel`'s `Scheme` from
  current focus + current `_background` whenever either changes. Rejected alternative: give
  `_emptyHintLabel` `CanFocus = true` and let it own its own focus scheme — rejected because the
  spec explicitly keeps the hint outside the list's focus/selection model, and giving it real
  focus would divert Tab/arrow handling into a control that's supposed to be inert.
- **Focused foreground is bright white against the unchanged dim background — not an inverted bar
  matching a real selected `ListView` row.** Originally tried mirroring a selected row's inverted
  fg/bg (and separately tried `VisualRole.Focus`/`.Disabled` directly), but `Terminal.Gui.Views.Label`
  turns out to only ever paint its own foreground; whatever background its container already
  painted shows through regardless of what's set via `SetScheme`. An inverted attribute therefore
  rendered as invisible dark-on-dark rather than a highlight bar. Bright white text against the
  same background `_emptyHintLabel` already had is the closest a `Label` can actually get to "looks
  focused."
- **Recompute the scheme in one place** (a small private method called from the focus handler,
  from `Background`'s setter, and from initial construction) so the three inputs — focus state,
  `_background` override, base scheme — never drift out of sync across call sites.

## Risks / Trade-offs

- [Terminal.Gui v2's focus-changed hook differs from v1's `Enter`/`Leave` events] → Resolved:
  `View.OnHasFocusChanged(bool, View?, View?)` is the v2 override point (backed by the
  `HasFocusChanged` event, `EventHandler<HasFocusEventArgs>`); `HasFocus` is recursively true on
  every SuperView while any focusable descendant is focused, so overriding it on
  `ListEditorView<T>` itself (rather than on `_listView`) correctly covers "focus is somewhere in
  this component."
- **Confirmed empirically, not just by inspection: giving `_emptyHintLabel` `CanFocus = true` is a
  hard blocker, not just a design preference.** Once a Terminal.Gui `Label` actually holds real
  keyboard focus, it swallows every subsequent key — not only Ctrl+N/E/D, but plain letters and
  even the app's Up/Down tab-navigation. Verified via a live run: with the hint focusable, `Ctrl+N`
  never reached `ListEditorView<T>.OnKeyDown` at all (confirmed by a temporary log-and-rebuild
  probe), while `_listView`-focused input worked as documented. This is why the hint stays
  `CanFocus = false` permanently and focus styling is driven by hand from `ListEditorView<T>`'s own
  `HasFocus`.
- **Found and fixed during verification: `OnHasFocusChanged` doesn't reliably fire when a modal
  (`TryCreate`/`TryEdit`'s `PatternDialog`/`HeaderDialog`) closes.** Repro: focus the empty list,
  Ctrl+N to add an item via the modal, then Ctrl+D to delete it back to empty — the hint reappeared
  with stale unfocused (dim) styling even though focus never left the component. Fixed by also
  recomputing the scheme from `UpdateEmptyHintVisibility` (i.e. on every item-collection change),
  not solely from the focus handler, so the display self-corrects the next time the hint's
  visibility is touched regardless of whether a focus event fired in between.
- **`Label` doesn't paint its own background, only its foreground.** Discovered while trying to
  match a real selected row's inverted look: setting an attribute with a different `Background` via
  `SetScheme` had no visible effect — the label's cells kept showing its container's background
  regardless. Confirmed by comparing raw ANSI captures of the computed attribute (logged directly
  from the running app) against what the terminal actually rendered: the background component never
  changed on screen even when the logged/computed value did. This is why the focused look is driven
  by foreground brightness alone.
- **Initial manual testing repeatedly gave contradictory colors before this was understood, which
  cost significant back-and-forth.** Root causes, once found: (1) the list already holds focus by
  default at app startup, so a first `Tab` press moves focus *away*, not into it — several rounds of
  "before/after Tab" captures had this backwards; (2) `_listView.GetAttributeForRole(...)` and
  `.GetScheme()` return not-yet-settled values very early in the view's lifecycle (e.g. background
  read as pure black before the view's first real layout pass, `#202020` after) — reading them from
  a `DrawingText` hook (tried as a "just recompute right before paint" fix) made this worse by
  lagging the actual paint by one frame, since `SetScheme` inside that handler doesn't reach the
  draw call already in progress. Recomputing from `OnHasFocusChanged` and `UpdateEmptyHintVisibility`
  (both firing well after initial layout has settled) avoids both problems.
