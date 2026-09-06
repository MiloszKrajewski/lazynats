## Why

The Publish tab is visually noisy: three full-width `SetScheme` band backgrounds (Subject,
Headers, Payload) plus the header list's own even/odd row striping compete for attention, with
no functional purpose behind the coloring. Separately, the header list is a bespoke inline
key/value editor (`HeaderListDataSource`, two always-visible `TextField`s, manual
New/Edit/Delete wiring) that duplicates the "New/Edit/Delete a list of items via modal" pattern
the app already has as a shared component (`ListEditorView<T>`, used by `SubscriptionsView`).
Routing headers through that shared component removes the duplication and, as a side effect,
deletes the header list's zebra striping outright rather than requiring it to be de-colored by
hand.

## What Changes

- Remove the three band-level `SetScheme` calls in `PublishView` (Subject/Headers/Payload); those
  regions inherit the ambient scheme like the rest of the app. The Subject field's red
  invalid-state color is unaffected — it's a functional signal, not decorative banding.
- **BREAKING**: Replace the inline header key/value editor with a nested
  `ListEditorView<HeaderPair>`. Instead of always-visible Key/Value fields where Ctrl+N clears the
  row and Enter appends, headers are now created/edited via a modal `HeaderDialog` (one text
  field, modeled on `PatternDialog`), seeded and formatted as `"Key: Value"` via a new
  `HeaderPresenter`. On commit, the text is split on the first `:` into Key/Value; there is no
  colon-required validation — only non-empty text is enforced, same level as today's Subject
  validity check. A missing colon is the user's responsibility, accepted as a simplicity
  trade-off.
- **BREAKING**: Delete `HeaderListDataSource` entirely (including its even/odd row coloring) along
  with `PublishView`'s `_headerKeyField`, `_headerValueField`, `_headerListView`, and the
  `ClearHeaderInput`/`LoadSelectedForEditing`/`CommitHeaderInput`/`RemoveSelectedHeader` methods.
- **BREAKING**: Header Ctrl+N/E/D now only fire when the nested header editor has focus, not
  tab-wide as today (the old tab-wide bindings on `PublishView` are removed; `ListEditorView`
  already binds these on itself).
- Out of scope: any future shared "greyish background for editable surfaces" convention
  (TextField/TextView/ListView) is a separate, later decision and is not implemented here.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `nats-publish`: header entry/edit/removal move from an inline always-visible key/value input
  row to a modal dialog via the shared list editor, changing the "Compose Message Fields",
  "Keyboard-Only Header Entry", "Keyboard-Only Header Edit", and "Keyboard-Only Header Removal"
  requirements. The decorative band/zebra coloring being removed has no dedicated spec scenarios
  today, so no additional requirement changes are needed for that part.

## Impact

- `src/lazynats/PublishView.cs`: header editing section rewritten to embed a
  `ListEditorView<HeaderPair>` subclass; band `SetScheme` calls removed.
- `src/lazynats/HeaderListDataSource.cs`: deleted.
- New files: a `HeaderDialog` (mirroring `src/lazynats/Subscriptions/PatternDialog.cs`), a
  `HeaderPresenter : IValuePresenter<HeaderPair>`, and a `HeaderEditorView : ListEditorView<HeaderPair>`
  subclass (mirroring `src/lazynats/Subscriptions/SubscriptionsView.cs`).
- `openspec/specs/nats-publish/spec.md`: requirement text updated to describe the modal-dialog
  header flow instead of the inline-row flow.
- No changes to `SubscriptionRegistry`, NATS connection handling, or the payload/subject send
  logic.
