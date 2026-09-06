## Why

The OBJ tab's bucket level only supports Create today, mirroring the KV tab except for Delete —
`ObjStore/BucketListView` deliberately hides `DrillableListView<T>`'s inherited `CreateRequested`
event with `new` because the shared `EnableCreateDelete()` wiring bundles Create and Delete
together, and OBJ previously wanted Create only. Now OBJ needs Delete too, so that hiding hack
needs to go and the base wiring needs to stop assuming Create and Delete always come as a pair.

## What Changes

- Split `DrillableListView<T>`'s `EnableCreateDelete()` into two independent opt-ins,
  `EnableCreate()` and `EnableDelete()`, each activatable on its own — Create alone, Delete alone,
  or both — with `Shortcuts` reporting exactly the hints for whichever are active.
- Update `KVStore/BucketListView` (currently the only caller of `EnableCreateDelete()`) to call
  both new methods, preserving its existing Create+Delete behavior unchanged.
- Remove the `new event Action? CreateRequested` hiding hack from `ObjStore/BucketListView` and
  have it activate `EnableCreate()` (as before) plus the new `EnableDelete()`.
- Add Delete Bucket to the OBJ tab's bucket level: Ctrl+D opens a confirmation prompt, then
  deletes the object store bucket on the server and refreshes the bucket list, mirroring the KV
  tab's `TryDeleteBucketAsync` exactly (confirm → delete → refresh with neighbor selected → error
  dialog on failure without losing list state).
- Update the `nats-obj` spec's "Read-Only Tab" requirement so it no longer claims the bucket level
  has no delete affordance.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `nats-obj`: the bucket level gains a Delete Bucket requirement (Ctrl+D, confirm, delete,
  refresh); the existing "Read-Only Tab" requirement is narrowed so it no longer claims bucket
  deletion is absent (object-level and bucket-edit restrictions are unchanged).
- `drillable-list`: the "Shared Create/Delete Wiring" requirement is replaced by two independent
  requirements, "Shared Create Wiring" and "Shared Delete Wiring", each activatable without the
  other.

## Impact

- `src/lazynats/Components/DrillableListView.cs`: split `EnableCreateDelete()`; update
  `Shortcuts` to track Create/Delete activation independently.
- `src/lazynats/KVStore/BucketListView.cs`: call both new opt-ins instead of the combined one.
- `src/lazynats/ObjStore/BucketListView.cs`: drop the `new event` hiding hack; activate
  `EnableCreate()` + `EnableDelete()`.
- `src/lazynats/ObjStore/ObjTab.cs`: wire `DeleteRequested` to a new `TryDeleteBucketAsync`,
  mirroring `KVStore/KvTab.cs`.
- `openspec/specs/nats-obj/spec.md`, `openspec/specs/drillable-list/spec.md`: requirement deltas
  described above.
