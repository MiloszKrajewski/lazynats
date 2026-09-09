## Why

The management UI is currently rendered in a single flat white-on-black palette, which makes it
hard to visually scan. `message-detail-dialog`/`kv-value-detail-dialog` (and the live feed) already
color-code an item's identifying text cyan (`Theme.SubjectColor`) and its metadata green, but the
drill-down lists that make up most of the management UI (streams, consumers, KV/OBJ buckets, keys,
objects, templates) still render in plain white. This is the first, deliberately small step in
extending that existing color convention outward - narrowed to just the drillable lists' own
identifiers for now, rather than recoloring the whole UI at once.

## What Changes

- Every `DrillableListView<T>`-based list (`StreamListView`, `ConsumerListView`, both `BucketListView`s
  (KV and OBJ), `KeyListView`, `ObjectListView`, `TemplateListView`) renders its row text - the
  item's identifying name - in `Theme.SubjectColor` (cyan), the same color already used for a
  message's subject and a KV key elsewhere in the app.
- `SubscriptionsView` (a `ListEditorView<T>`-based list, but a pure identifier list like the
  drillable lists above - each row is just a subject pattern) also renders its row text in
  `Theme.SubjectColor`.
- `HeaderEditorView` (also `ListEditorView<T>`-based) is explicitly out of scope for this change
  and keeps its current plain-white rendering - a header row is a key/value pair, not a single
  identifier, so the same coloring wouldn't read the same way.
- No new theme color is introduced; this reuses the existing `Theme.SubjectColor` constant.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `drillable-list`: adds a requirement that a drillable list's rendered row text uses the app's
  identifier color, rather than leaving color unspecified as a plain-text-only render.
- `list-editor`: adds a per-subclass opt-in for the same identifier-color row text, used by
  `SubscriptionsView` and left off by `HeaderEditorView`.

## Impact

- `src/lazynats/Components/PresenterListDataSource.cs` - render an item's formatted text in an
  identifier color when the owning list opts in, instead of always drawing plain text.
- `src/lazynats/Components/DrillableListView.cs` - opts its shared `PresenterListDataSource<T>`
  instance into the identifier color unconditionally (every drillable list).
- `src/lazynats/Components/ListEditorView.cs` - gains the same optional `textColor` constructor
  parameter (defaulted `null`), passed through to its own `PresenterListDataSource<T>`.
- `src/lazynats/Subscriptions/SubscriptionsView.cs` - passes `Theme.SubjectColor`.
- `src/lazynats/Publish/HeaderEditorView.cs` - unchanged; keeps the `null` default, so its rows
  keep rendering as plain text.
- No changes to `Theme.cs` (reuses `SubjectColor`).
