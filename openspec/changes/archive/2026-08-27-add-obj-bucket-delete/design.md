## Context

`DrillableListView<T>.EnableCreateDelete()` binds Ctrl+N and Ctrl+D together as a single opt-in,
on the assumption that any list wanting one wants both. `KVStore/BucketListView` (the only current
caller) does want both. `ObjStore/BucketListView` wanted Create only, so it couldn't call
`EnableCreateDelete()` without also getting a Delete affordance the "Read-Only Tab" requirement
explicitly forbade — instead it declared its own `public new event Action? CreateRequested` to
shadow the inherited (unused) one and wired Ctrl+N itself, bypassing the shared method entirely.
Now OBJ needs Delete too, which removes the reason for the workaround, but the underlying design
flaw (Create and Delete forced to activate together) would otherwise resurface for the next list
that wants only one of the two.

## Goals / Non-Goals

**Goals:**
- Let a `DrillableListView<T>` subclass activate Create and Delete independently.
- Remove `ObjStore/BucketListView`'s `new`-hiding workaround entirely.
- Add bucket deletion to the OBJ tab, behaviorally identical to the KV tab's.

**Non-Goals:**
- No change to object-level (drilled-into-bucket) behavior — it stays fully read-only per
  `nats-obj`'s existing "Read-Only Tab" scenario for that level.
- No change to `StreamsTab`/`ConsumerListView` or any other `DrillableListView<T>` consumer beyond
  `KVStore/BucketListView` and `ObjStore/BucketListView`.
- No generalization beyond Create/Delete (e.g. a fully generic command-registration system) —
  splitting the one pairing that's actually forced today is enough.

## Decisions

- **Two opt-in methods, `EnableCreate()` and `EnableDelete()`, replacing `EnableCreateDelete()`.**
  Each binds its own key (Ctrl+N / Ctrl+D) and command, and tracks its own activation flag so
  `Shortcuts` can report "New" and/or "Delete" independently. `KVStore/BucketListView` calls both
  (unchanged net behavior); `ObjStore/BucketListView` calls both now too, but as two separate,
  independently-justified calls rather than one bundled call plus a workaround.
  Alternative considered: keep `EnableCreateDelete()` and add a parameter (e.g.
  `EnableCreateDelete(delete: false)`). Rejected — a boolean flag on a method named "CreateDelete"
  is exactly the kind of implicit pairing this change is meant to eliminate; two independently-
  named methods make the activation explicit at each call site.
- **`ObjStore/BucketListView` drops its `new event Action? CreateRequested` entirely** and relies
  on the base class's inherited `CreateRequested`/`DeleteRequested`, same as `KVStore/
  BucketListView` does. This removes the last consumer of the hiding pattern flagged in
  `feedback_no_event_hiding_hack` memory.
- **`ObjTab.TryDeleteBucketAsync` mirrors `KvTab.TryDeleteBucketAsync` line-for-line** (confirm
  dialog → `_obj.DeleteObjectStoreAsync(name)` → refresh with neighbor selected → error
  `MessageBox` on failure, list left untouched). No shared helper is extracted for this — the two
  tabs already duplicate the equivalent Create flow (`OpenCreateBucketDialog`/
  `TryCreateBucketAsync`) without a shared abstraction, so Delete follows the same precedent
  rather than introducing an inconsistent abstraction just for this one method.

## Risks / Trade-offs

- [Splitting the opt-in touches the only other caller (`KVStore/BucketListView`)] → Low risk: its
  new call sequence (`EnableCreate(); EnableDelete();`) is a mechanical rename with no behavior
  change, and the `drillable-list` spec's existing Create/Delete scenarios are preserved verbatim
  under the two new requirement names.
- [Bucket deletion is irreversible and, per `nats-obj`, invalidates any objects in the bucket] →
  Mitigated the same way KV already mitigates it: a confirmation dialog naming the bucket and
  warning the action cannot be undone, matching `KvTab`'s existing wording.
