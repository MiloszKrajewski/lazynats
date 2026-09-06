## Why

Streams, consumers, and KV/OBJ buckets can currently only be created or deleted from lazynats —
fixing a wrong Max Age, adding a filter subject to a consumer, or adjusting a bucket's History
means dropping to the `nats` CLI or deleting and recreating the resource (losing its data) inside
the app itself. Every affected list already has a matching "New" dialog with the right fields and
validation; the fields the server actually allows to change after creation are a subset of what
that dialog already collects.

## What Changes

- Each "New X" dialog (`CreateStreamDialog`, `CreateConsumerDialog`, `KVStore/CreateBucketDialog`,
  `ObjStore/CreateBucketDialog`) gains an `isEdit` mode: title and confirm-button text switch to
  "Edit"/"_Save", and fields the server won't accept a change to are disabled (shown, not hidden,
  so the current value stays visible) rather than removed.
- `DrillableListView<T>` gains a shared, opt-in `EnableEdit()` wiring (`EditRequested` event,
  Ctrl+E binding, "Edit" shortcut hint) mirroring the existing `EnableCreate`/`EnableDelete` shape.
- Streams tab: Ctrl+E on the stream list edits Subjects and Max Age (Name and Retention are
  locked). Ctrl+E on the consumer list edits Filter Subjects (Name, Ack Policy, and Deliver Policy
  are locked — confirmed server-rejected via a live NATS 2.12.8 test, error code 10012).
- KV tab: Ctrl+E on the bucket list edits History, Max Age, and Limit Marker TTL (Name and Storage
  are locked).
- OBJ tab: Ctrl+E on the bucket list edits Max Age (Name is locked). `NATS.Client.ObjectStore`
  exposes no bucket-level update call, so this goes through `INatsJSContext.UpdateStreamAsync`
  against the underlying `OBJ_<bucket>` stream directly — the same layer `ObjTab` already reaches
  into for `ListStreamsAsync` (there's no bucket-listing call on `INatsObjContext` either).
- Every edit failure (server rejection, connection error, ...) is caught, shown in a modal error
  dialog using the exception's message as-is (no deeper diagnosis), and the edit dialog reopens
  pre-filled with the attempted values so the user can adjust or press Esc to back out — matching
  the existing Create-failure behavior exactly.
- Key-level and object-level entries (KV keys, OBJ objects) remain fully read-only; this change
  does not touch them.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `drillable-list`: "Read-Only Presentation" no longer holds unconditionally — a new opt-in Edit
  wiring (mirroring Create/Delete) is added, activatable independently of the other opt-in shapes.
- `nats-streams`: "Read-Only List" is replaced by Edit support at both the stream and consumer
  levels, each with its own field-level lock set and validation.
- `nats-kv`: "Read-Only Tab" is narrowed to still forbid key-level mutation, but bucket-level Edit
  is now provided alongside Create/Delete.
- `nats-obj`: "Read-Only Tab" is narrowed to still forbid object-level mutation, but bucket-level
  Edit is now provided alongside Create/Delete.

## Impact

- `Components/DrillableListView.cs`: new `EnableEdit()`/`EditRequested`/Ctrl+E shape and shortcut
  hint, same style as the existing `EnableCreate`/`EnableDelete`.
- `Streams/CreateStreamDialog.cs`, `Streams/CreateConsumerDialog.cs`,
  `KVStore/CreateBucketDialog.cs`, `ObjStore/CreateBucketDialog.cs`: `isEdit` constructor
  parameter, per-field `Enabled` toggling, title/button text switch.
- `Streams/StreamsTab.cs`, `KVStore/KvTab.cs`, `ObjStore/ObjTab.cs`: wire `EditRequested` to a new
  `OpenEdit*Dialog`/`TryEdit*Async` pair per level, following the existing
  `OpenCreate*Dialog`/`TryCreate*Async` pair shape (including reopen-with-seed on failure).
  `ObjTab` additionally needs `NATS.Client.JetStream.Models.StreamConfig` round-tripping for its
  bucket-level edit, since it edits the underlying stream directly.
- No new NuGet dependencies. No changes to key-level or object-level (KV/OBJ) code.
