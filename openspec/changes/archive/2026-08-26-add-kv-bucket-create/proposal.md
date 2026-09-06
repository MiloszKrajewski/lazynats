## Why

The KV tab is currently read-only by design (`nats-kv`'s "Read-Only Tab" requirement) — a KV
bucket has to be created with the `nats` CLI or another client before it shows up here. That's
the same CLI-round-trip gap the Streams tab closed for streams (`add-stream-create`); closing it
for buckets brings the KV tab to parity with a common setup step.

## What Changes

- Add a Ctrl+N binding on the KV tab's bucket-level list that opens a "New Bucket" modal dialog.
- The dialog collects the minimal set of fields needed to create a usable bucket: Name, Storage
  backend (File/Memory), History (max revisions kept per key), Max Age, and Limit Marker TTL.
  Storage is included alongside the other fields (rather than deferred to Advanced) because
  Memory-backed buckets are a distinct, common use case — fast, ephemeral caches — not a rarely
  tuned knob, so picking it at creation time (when the backend is fixed for the bucket's
  lifetime — `NatsKVContext.UpdateStoreAsync`'s own doc says storage type cannot change) matters
  more than most of what's deferred. Limit Marker TTL is included alongside Max Age (rather than
  deferred to Advanced) because it governs whether a delete/purge marker is left behind when
  TTL-based removal is used — for a bucket left with unlimited Max Age, per-key TTL is the only
  expiry mechanism available, so this setting is what determines whether other clients can tell a
  key was actively removed versus never having existed. History is included alongside the other
  essential fields (a pivot from this change's original scope, which deferred it to Advanced) —
  unlike Storage it isn't fixed for the bucket's lifetime (`NatsKVContext.UpdateStoreAsync`'s own
  doc places no restriction on changing it later), but users creating a bucket for a
  multi-revision use case (an audit trail, a small event log keyed by ID) want to set it at
  creation time rather than immediately need an Advanced/Edit surface that doesn't exist yet;
  left unset, it still defaults to 1 (keep only the latest revision per key), preserving this
  change's original behavior for users who don't touch the field. Everything else in
  `NatsKVConfig` (max value size, replicas, compression, mirrors/sources, republish, ...) is
  likewise left at safe explicit defaults for this slice and deferred to a future "Advanced"
  section of the dialog.
- On commit, the dialog calls `INatsKVContext.CreateStoreAsync` and the bucket list is refreshed
  to include the new bucket, which becomes highlighted.
- **BREAKING** (spec-level, not code): supersedes `nats-kv`'s "Read-Only Tab" requirement at the
  bucket level — the key level remains fully read-only; only bucket creation is added.

## Capabilities

### New Capabilities

(none — this extends the existing KV tab rather than introducing a new management surface)

### Modified Capabilities

- `nats-kv`: adds a "Create Bucket" requirement (Ctrl+N, dialog fields, validation, commit
  behavior) and narrows "Read-Only Tab" so it no longer claims the bucket level has zero mutation
  affordances — the key level's read-only guarantee is unchanged.

## Impact

- New `KVStore/NewBucketOptions.cs`: a dialog-owned result record (nullable fields for "unset"),
  kept separate from `NATS.Client.KeyValueStore.NatsKVConfig`'s wire representation, plus a
  `ToNatsKVConfig()` translation used only at the `CreateStoreAsync` call site — mirrors
  `Streams/NewStreamOptions.cs`'s split for the same reason (CLR-default sentinels on the wire
  type don't mean "unset").
- New `KVStore/CreateBucketDialog.cs` (`Dialog<NewBucketOptions>`), following
  `Streams/CreateStreamDialog.cs`'s multi-field/validation/retry-on-failure shape.
- `KVStore/BucketListView.cs` gains its own Ctrl+N command (mirroring `StreamListView`'s layering
  of `Command.New` on top of the shared `DrillableListView<T>` base), raising a
  `CreateRequested` event.
- `KVStore/KvTab.cs` handles `CreateRequested`: run the dialog, translate via `ToNatsKVConfig()`,
  call `_kv.CreateStoreAsync`, refresh `BucketListView` on success, `MessageBox.ErrorQuery` +
  reopen-seeded on failure — same shape as `StreamsTab`'s existing create-stream handling.
- Depends on `NATS.Client.KeyValueStore.INatsKVContext.CreateStoreAsync(NatsKVConfig,
  CancellationToken)` (already referenced elsewhere in the project via `NATS.Client.KeyValueStore`
  2.8.2; no new package).
