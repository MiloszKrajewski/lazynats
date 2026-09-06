## Why

`StreamListView`/`ConsumerListView` and `StreamDetails`/`ConsumerDetails` are near-duplicate
pairs (list-pane plumbing and details-pane polling plumbing, respectively), and `doc/UI.md`'s
planned KV and OBJ management tabs need the exact same bucket-to-entries drill-down shape.
Extracting the shared plumbing into two reusable base classes now — while there is only one pair
to refactor — avoids paying the duplication cost three more times over.

## What Changes

- Introduce `DrillableListView<T>` (abstract, `Components/`): shared list-pane plumbing (item/
  data-source/list-view wiring, empty-hint mechanics, `Background`, Ctrl+R → Refresh, identity-
  preserving `ReplaceItems`). `Presenter`, `EmptyHintText`, and `GetIdentity` are required
  overrides. Level-specific behavior (Enter→Descend, Esc/Backspace→Ascend, and any future
  per-level command such as a KV-key list's eventual Ctrl+N) stays in each concrete subclass, not
  the base — list behavior is driven by list type, not item type.
- Introduce `PollingDetailsView<TTarget, TInfo>` (abstract, `Components/`): shared details-pane
  plumbing (`Show`/`SetTarget`/`SetActive` wiring, the lazily-started active-gated poll pipeline,
  the label:value row renderer). `FetchAsync` and `BuildRows` are required overrides. A `virtual
  TimeSpan? PollInterval` (default `3s`) hook lets a future subclass whose target never changes
  after first load (e.g. a single live-feed message) opt out of polling entirely by returning
  `null`.
- Refactor `StreamListView`/`ConsumerListView` to derive from `DrillableListView<T>`.
- Refactor `StreamDetails`/`ConsumerDetails` to derive from `PollingDetailsView<TTarget, TInfo>`.
- `StreamsTab`'s two-slot orchestration (`Descend`/`Ascend`, visibility toggling,
  `RefreshListAsync`/`RefreshConsumerListAsync`) is unchanged — it stays hand-rolled per tab.
- `ListEditorView<T>` is explicitly left as-is: it shares some plumbing shape with the new base
  classes (empty-hint mechanics, `Background`, presenter-formatted data source) but is not
  refactored onto a common foundation with them in this change.
- No observable behavior change: this is a pure internal refactor. The Streams tab's behavior
  stays exactly as specified in `nats-streams`.

## Capabilities

### New Capabilities
- `drillable-list`: reusable base class contract for a read-only, presenter-formatted,
  Ctrl+R-refreshable list with identity-preserving replace and an empty-state hint — the shape
  shared today by `StreamListView`/`ConsumerListView` and needed next by the planned KV/OBJ tabs.
- `polling-details`: reusable base class contract for a label:value detail panel driven by a
  lazily-started, active-gated poll pipeline, with an overridable hook to disable polling
  entirely for a subclass whose data never changes after load.

### Modified Capabilities
(none — `nats-streams`'s requirements are unchanged; this change only relocates how they're
implemented)

## Impact

- New files: `src/lazynats/Components/DrillableListView.cs`, `src/lazynats/Components/PollingDetailsView.cs`.
- Refactored: `src/lazynats/Streams/StreamListView.cs`, `ConsumerListView.cs`, `StreamDetails.cs`,
  `ConsumerDetails.cs`.
- Unchanged: `src/lazynats/Streams/StreamsTab.cs`, `src/lazynats/Components/ListEditorView.cs`,
  all other tabs.
- No new dependencies, no public API surface beyond the two new internal base classes, no
  behavior change visible to the user. Sets up the shared foundation the future KV/OBJ tabs
  (not part of this change) will build on.
