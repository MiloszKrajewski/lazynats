## Why

The OBJ tab is currently fully read-only by design (`nats-obj`'s "Read-Only Tab" requirement) — an
Object Store bucket has to be created with the `nats` CLI or another client before it shows up
here. This is the same CLI-round-trip gap `add-kv-bucket-create` closed for KV buckets; closing it
for OBJ buckets brings this tab to the same level of parity with a common setup step.

## What Changes

- Add a Ctrl+N binding on the OBJ tab's bucket-level list that opens a "New Bucket" modal dialog,
  mirroring `KVStore/CreateBucketDialog.cs`'s shape.
- The dialog collects the minimal set of fields needed to create a usable bucket: Name and Max
  Age. Max Age is included because it's the primary per-object expiry control and the direct OBJ
  analog of KV's own Max Age field. Storage backend (File/Memory) is deliberately left out of
  this slice, unlike `add-kv-bucket-create`'s dialog — a memory-backed Object Store bucket is a
  rare choice in practice (unlike memory-backed KV, a common ephemeral-cache pattern), so it does
  not earn a place among the core fields; every bucket created via this dialog uses the default
  File backend, with Storage deferred to a future "Advanced" section alongside Description, Max
  Bytes, Number of Replicas, Placement, Metadata, and Compression. Object Store also has no analog
  of KV's History/Limit Marker TTL fields (an object is not a keyed, revisioned entry), so this
  dialog ends up with just two fields.
- On commit, the dialog calls `INatsObjContext.CreateObjectStoreAsync` and the bucket list is
  refreshed to include the new bucket, which becomes highlighted.
- **BREAKING** (spec-level, not code): supersedes `nats-obj`'s "Read-Only Tab" requirement at the
  bucket level — the object level remains fully read-only; only bucket creation is added.

## Capabilities

### New Capabilities

(none — this extends the existing OBJ tab rather than introducing a new management surface)

### Modified Capabilities

- `nats-obj`: adds a "Create Bucket" requirement (Ctrl+N, dialog fields, validation, commit
  behavior) and narrows "Read-Only Tab" so it no longer claims the bucket level has zero mutation
  affordances — the object level's read-only guarantee is unchanged.

## Impact

- New `ObjStore/NewBucketOptions.cs`: a dialog-owned result record (nullable fields for "unset"),
  kept separate from `NATS.Client.ObjectStore.NatsObjConfig`'s wire representation, plus a
  `ToNatsObjConfig()` translation used only at the `CreateObjectStoreAsync` call site — mirrors
  `KVStore/NewBucketOptions.cs`'s split for the same reason (CLR-default sentinels on the wire
  type don't mean "unset").
- New `ObjStore/CreateBucketDialog.cs` (`Dialog<NewBucketOptions>`), following
  `KVStore/CreateBucketDialog.cs`'s multi-field/validation/retry-on-failure shape.
- `ObjStore/BucketListView.cs` gains its own Ctrl+N command (mirroring `KVStore/BucketListView.cs`
  layering `Command.New` on top of the shared `DrillableListView<T>` base), raising a
  `CreateRequested` event.
- `ObjStore/ObjTab.cs` handles `CreateRequested`: run the dialog, translate via
  `ToNatsObjConfig()`, call `_obj.CreateObjectStoreAsync`, refresh `BucketListView` on success,
  `MessageBox.ErrorQuery` + reopen-seeded on failure — same shape as `KvTab`'s existing
  create-bucket handling.
- Depends on `NATS.Client.ObjectStore.INatsObjContext.CreateObjectStoreAsync(NatsObjConfig,
  CancellationToken)` (already referenced elsewhere in the project via `NATS.Client.ObjectStore`
  2.8.2; no new package).
