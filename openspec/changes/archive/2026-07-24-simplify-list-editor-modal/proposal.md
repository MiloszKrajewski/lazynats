## Why

`ListEditorView<T>` couples a permanent text-input control to the list so Enter can append/commit
free-text edits in place. That coupling is why arrow-key navigation between the input and the list
needed its own spec requirement (Up at the top row focuses the input) and its own bookkeeping
(`_editingIndex`, `ClearInput`, live parse-validation) — and it broke again once `Tabs`' own
arrow-key handling changed underneath it. Reducing the list editor to just the list, and invoking a
modal dialog for New/Edit instead of an inline input, removes that whole coordination problem
rather than patching it again, and gives Streams/Consumers/KV/OBJ tabs (not yet built) a richer
editing surface than a single text line when their items need more than one field.

## What Changes

- **BREAKING**: `ListEditorView<T>` drops its inline `TextField` input entirely. Ctrl+N and Ctrl+E
  no longer populate/clear an input in place; each instead invokes an abstract, presenter-free
  callback that runs a modal `Dialog` and reports success via a `bool` return plus an `out T`
  result — mirroring the `TryParse`-style idiom already used by `IValuePresenter<T>`:
  - `protected abstract bool TryCreate(out T result)` (Ctrl+N)
  - `protected abstract bool TryEdit(T original, out T result)` (Ctrl+E, called with the selected
    item)
  - Returning `false` (dialog cancelled) leaves the item collection unchanged.
- **BREAKING**: `IValuePresenter<T>` drops `TryParse` — with no inline input left to validate live
  or parse on Enter, only `Format` (used for list-row rendering) remains meaningful.
- Remove now-unreachable `list-editor` mechanics: live invalid-input flagging, `Ctrl+N`-cancels-
  in-progress-edit semantics, and the Up-at-top-of-list-focuses-input requirement (there is no
  second focusable control left to focus).
- `SubscriptionsView` reimplements its Ctrl+E "change a pattern" flow as a modal (pattern text
  field pre-filled from the selected subscription) instead of loading the shared input; committing
  still removes the old subscription and adds a new one under a new identity, unchanged.

## Capabilities

### Modified Capabilities
- `list-editor`: replace the text-input-plus-list shape and its Enter-commits/live-validation/
  Ctrl+N-cancels-edit requirements with a list-only view whose New/Edit actions invoke an
  overridable modal callback (`TryCreate`/`TryEdit`) instead.
- `nats-subscriptions`: update the "Modifying a Subscription Pattern" requirement so Ctrl+E opens a
  modal pre-filled with the selected pattern rather than loading it into a shared input field.

## Impact

- `src/lazynats/Components/ListEditorView.cs`: remove `_inputField`, `_editingIndex`,
  `ClearInput`, live-validation, and the Up-focuses-input key binding; add `TryCreate`/`TryEdit`
  abstract members and wire Ctrl+N/Ctrl+E to them.
- `src/lazynats/Components/IValuePresenter.cs`: drop `TryParse`; interface becomes `Format`-only.
- `src/lazynats/Components/PresenterListDataSource.cs`: unaffected in structure, still calls
  `Format` for row rendering.
- `src/lazynats/Subscriptions/SubscriptionsView.cs`: replace `Append`/`Delete` overrides tied to
  input text with `TryCreate`/`TryEdit` overrides that build and run a small pattern-entry
  `Dialog`.
- `src/lazynats/Subscriptions/SubscriptionPatternPresenter.cs`: shrinks to `Format` only; pattern
  validation moves into the new dialog.
- `openspec/specs/list-editor/spec.md`, `openspec/specs/nats-subscriptions/spec.md`: requirement
  updates described above.
- Future Streams/Consumers/KV/OBJ tabs inherit the modal-callback shape instead of the retired
  inline-input one.
