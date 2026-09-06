## Why

The KV tab's bucket level can create buckets (`nats-kv`'s "Create Bucket") but has no way to
remove one — a bucket created by mistake, or one that's simply outlived its use, has to be deleted
with the `nats` CLI or another client, the exact round-trip lazynats exists to avoid. `nats-kv`'s
"Read-Only Tab" requirement currently rules this out entirely at the bucket level.

## What Changes

- Add a Ctrl+D binding on the KV tab's bucket-level list that deletes the highlighted bucket,
  after a confirmation prompt naming the bucket — same shape as the existing stream-level "Delete
  Stream" and consumer-level "Delete Consumer" (destructive, irreversible server-side operation;
  Cancel is the default, Enter-activated response).
- Confirming calls `INatsKVContext.DeleteStoreAsync(bucket)` and refreshes the bucket list so the
  deleted bucket disappears; cancelling (Esc, or choosing Cancel) leaves the list untouched.
- **BREAKING** (spec-level, not code): narrows `nats-kv`'s "Read-Only Tab" requirement, which
  currently states the bucket level provides no edit-or-delete affordance at all — it gains delete
  (edit remains out of scope). The key level's read-only guarantee is untouched.

## Capabilities

### New Capabilities

(none — this extends the existing KV tab)

### Modified Capabilities

- `nats-kv`: adds a "Delete Bucket" requirement (Ctrl+D, confirmation prompt naming the bucket,
  commit/cancel/failure behavior, highlight-moves-to-neighbor-on-delete) mirroring the existing
  stream/consumer delete requirements in `nats-streams`, and narrows "Read-Only Tab" so it no
  longer claims the bucket level has zero mutation affordances.

## Impact

- `KVStore/BucketListView.cs` gains its own `Command.DeleteAll` + `Key.D.WithCtrl` binding
  (mirroring `Streams/StreamListView.cs`'s existing `Command.New`/Ctrl+N and
  `Command.DeleteAll`/Ctrl+D layered on the shared `DrillableListView<T>` base), raising a
  `DeleteRequested` event.
- `KVStore/KvTab.cs` handles `DeleteRequested`: confirm via `MessageBox.Query`, call
  `_kv.DeleteStoreAsync(name)`, refresh the bucket list on success, show `MessageBox.ErrorQuery`
  on failure — same shape as `StreamsTab`'s `TryDeleteStreamAsync`/`NeighborStreamName` pair.
- Depends on `NATS.Client.KeyValueStore.INatsKVContext.DeleteStoreAsync(string,
  CancellationToken)` (already available in the referenced package version; no new dependency).
- No new files — this is additive wiring on the two existing KV-tab files.
