## Why

Six `DrillableListView<T>` subclasses (`StreamListView`, `ConsumerListView`,
`KVStore.BucketListView`, `KVStore.KeyListView`, `ObjStore.BucketListView`,
`ObjStore.ObjectListView`) hand-roll the same three wiring shapes — descend-on-Enter,
ascend-on-Esc/Backspace, and create/delete-on-Ctrl+N/Ctrl+D — each duplicated identically three
times, plus `StreamsTab`/`KvTab` each hand-roll an identical post-delete "which neighboring item
should get focus" index scan against a collection `DrillableListView<T>` already owns internally.
This is the same rule-of-three precedent the codebase already applied once (see
`openspec/changes/archive/2026-08-21-add-consumer-drilldown/design.md` decision #1, which
deliberately deferred extracting a shared base for `ConsumerListView`/`ConsumerDetails` until a
3rd occurrence arrived): each of these four shapes now has 3 near-identical copies, so extraction
stops being speculative.

## What Changes

- Add `NeighborIdentity(string identity)` to `DrillableListView<T>`, built on the existing private
  `IndexOfIdentity`/`GetIdentity`: returns the identity of the item after `identity` in the
  current list, or the one before it if `identity` is last, or `null` if `identity` isn't present
  or removing it would empty the list.
- Add three opt-in wiring helpers to `DrillableListView<T>`, each called from a subclass
  constructor to activate that subclass's navigation shape:
  - `EnableDescend()` — adds `DescendRequested` and wires `ListView.Accepted` to raise it.
  - `EnableAscend()` — adds `AscendRequested`, binds Esc/Backspace to raise it, and folds an Esc
    "Back" hint into `Shortcuts`.
  - `EnableCreateDelete()` — adds `CreateRequested`/`DeleteRequested`, removes the inner
    `ListView`'s Ctrl+N-aliases-Down binding, binds Ctrl+N/Ctrl+D to raise them, and folds "New"/
    "Delete" hints into `Shortcuts`.
  A subclass composes at most one of `EnableDescend`/`EnableAscend` plus, optionally,
  `EnableCreateDelete` — the three stay independent, so this does not collapse the way a
  hierarchy-level (parent/child) abstraction would at a 3rd drill level.
- Update all six subclasses to call the relevant helper(s) instead of hand-wiring the same
  `AddCommand`/`KeyBindings.Add`/`Shortcuts.Append` triplets.
- Update `StreamsTab.TryDeleteStreamAsync`, `StreamsTab.TryDeleteConsumerAsync`, and
  `KvTab.TryDeleteBucketAsync` to call `_listView.NeighborIdentity(name)` instead of their own
  `NeighborStreamName`/`NeighborConsumerName`/`NeighborBucketName`, and delete those three methods.
- No observable behavior changes: key bindings, shortcut hints, and event timing are unchanged —
  this only moves where the wiring code lives.

**Explicitly out of scope** (logged as future goals, not part of this change):
- Extracting a shared tab-level scaffold across `StreamsTab`/`KvTab`/`ObjTab` (their
  `OnHasFocusChanged`/`Descend`/`Ascend`/field-set duplication) — considered and rejected this
  round; a parent/child-shaped base would need a redesign, not a mechanical extraction, if a 3rd
  drill level (beyond bucket→key, stream→consumer) ever appears.
- The create-dialog retry loop duplicated in `StreamsTab`/`KvTab` (`OpenCreate*Dialog`/
  `TryCreate*Async`).
- The delete-with-confirm duplication in `StreamsTab`/`KvTab` (`TryDelete*Async`'s
  confirm-dialog/delete-call/error-handling shell) — only its neighbor-lookup sub-step moves in
  this change; the surrounding method stays as-is.
- The `Refresh*ListAsync` try/fetch/`ReplaceItems`/catch/`StatusChanged` shell duplicated six
  times across `StreamsTab`/`KvTab`/`ObjTab`.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `drillable-list`: `DrillableListView<T>` gains a `NeighborIdentity` query and three opt-in
  navigation-wiring helpers (`EnableDescend`/`EnableAscend`/`EnableCreateDelete`). The existing
  "Subclass-Defined Navigation Commands" requirement (base binds no key beyond Ctrl+R; each
  subclass independently defines its own extra bindings) is updated: the base may now supply
  shared, opt-in implementations of the three common navigation shapes, but a given instance's
  behavior still depends only on which helpers *that* subclass's constructor calls — one
  subclass's choice to opt in still has no effect on another subclass's behavior or the base's
  default (Ctrl+R-only) shape.

## Impact

- `src/lazynats/Components/DrillableListView.cs` — add `NeighborIdentity` and the three
  `Enable*` helpers.
- `src/lazynats/Streams/StreamListView.cs`, `src/lazynats/Streams/ConsumerListView.cs`,
  `src/lazynats/KVStore/BucketListView.cs`, `src/lazynats/KVStore/KeyListView.cs`,
  `src/lazynats/ObjStore/BucketListView.cs`, `src/lazynats/ObjStore/ObjectListView.cs` — replace
  hand-rolled wiring with the relevant `Enable*` call(s).
- `src/lazynats/Streams/StreamsTab.cs`, `src/lazynats/KVStore/KvTab.cs` — delete
  `Neighbor*Name` methods, call `NeighborIdentity` instead.
- No API/behavior surface visible to the user changes; no new dependencies.
