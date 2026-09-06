## Why

`ListEditorView<T>`'s standalone/modal mode (`bindSharedKeys: true`) binds New/Edit/Delete to
`Ctrl+N`/`Ctrl+E`/`Ctrl+D` instead of the bare letters its tab-hosted mode uses, on the theory
that the modal it shares (e.g. `PublishDialog`) has other free-text fields that bare letters would
collide with. In practice the only standalone consumer, `HeaderEditorView`, has no such
colliding descendant (headers are edited via a separate modal dialog, not in place), so the Ctrl
modifier buys nothing there. Worse, its Filter binding (`Ctrl+F`) is registered unconditionally
whenever `bindSharedKeys` is true, regardless of whether the subclass ever calls `EnableFilter()`
— `HeaderEditorView` never does (a header list realistically holds 1-2 entries; filtering it was
never useful), so today `Ctrl+F` silently opens a filter dialog for headers despite no "Filter"
hint ever being advertised for it.

## What Changes

- **BREAKING** (user-facing key change): `ListEditorView<T>`'s standalone/modal mode binds plain
  `N`/`E`/`D` instead of `Ctrl+N`/`Ctrl+E`/`Ctrl+D`. Tab-hosted mode is unchanged (already plain).
- Filter's key binding (plain `F`, same change as above) is registered only when the subclass has
  called `EnableFilter()`, matching the already-opt-in "Shared Filter Wiring" behavior — fixing
  `HeaderEditorView`'s unconditional, unadvertised `Ctrl+F`. `HeaderEditorView` doesn't opt in, so
  headers get no Filter binding at all, standalone or otherwise.
- `bindSharedKeys` itself is unchanged: it still determines *where* the binding lives (directly on
  the component vs. hoisted to an owning tab via `TabOperations`) — only the key chord and the
  Filter gating change.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `list-editor`: "Key Bindings Work Regardless of Focused Child" changes from Ctrl-modified keys
  in standalone usage to the same bare letters tab-hosted usage already uses; Filter's binding
  becomes conditional on the subclass opting in, in both usage modes.

## Impact

- `src/lazynats/Components/ListEditorView.cs`: key binding registration, the `Shortcuts` property's
  hint list, and the base `EmptyHint` default text (also references `Ctrl+N`).
- `src/lazynats/Publish/HeaderEditorView.cs`: its `EmptyHint` override references `Ctrl+N` and
  needs updating to `N`.
- No change to `SubscriptionsView` or any tab-hosted usage (already bare letters).
