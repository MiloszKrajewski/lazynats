## Why

The KV tab's key level is currently read-only (`openspec/specs/nats-kv/spec.md`'s "Read-Only
Tab" requirement) — the only way to create, edit, or delete a key/value pair is outside the app
(e.g. the `nats` CLI). Bucket-level CRUD already exists (Create/Edit/Delete Bucket), so the tab
manages buckets but not what's inside them. Adding key-level CRUD closes that gap for the common
case of small string values, without yet building the "smart" text/hex editor a fully general
binary-safe editor would need.

## What Changes

- **BREAKING**: Removes the "Read-Only Tab" requirement — the key level gains Ctrl+N/Ctrl+E/
  Ctrl+D affordances, mirroring the bucket level's Create/Edit/Delete shape exactly (same
  `DrillableListView` `EnableCreate`/`EnableEdit`/`EnableDelete` shared shapes `BucketListView`
  already uses).
- Adds **Create Key** (Ctrl+N at the key level): a modal dialog with a Name field and a
  multi-line Value field, writing a new entry via `PutAsync`.
- Adds **Edit Key** (Ctrl+E at the key level): the same dialog in edit mode (Name locked, Value
  seeded from the current entry's decoded text), overwriting the entry via `PutAsync`. No
  optimistic-concurrency (revision-based CAS) check — last write wins, same simplification the
  existing Edit Bucket flow makes for bucket config.
- Adds **Delete Key** (Ctrl+D at the key level): a confirm-first prompt, deleting via
  `DeleteAsync` (soft delete/tombstone — the standard NATS KV semantics, not a history-erasing
  purge).
- Adds a printable-text guard: a key whose current value isn't valid, printable UTF-8 text
  (i.e. looks like binary data) cannot be opened for editing — Ctrl+E on such a key reports a
  status message instead of opening the dialog. Create is unaffected, since a value typed into
  the dialog is always text by construction. This is a deliberate stand-in for a future "smart"
  text/hex editor, not a permanent restriction.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-kv`: removes "Read-Only Tab"; adds Create Key, Edit Key, Delete Key, and the printable-
  text edit guard, at the key level — mirroring the existing bucket-level requirements of the
  same shape.

## Impact

- `src/lazynats/KVStore/KeyListView.cs`: opt into `EnableCreate`/`EnableEdit`/`EnableDelete`,
  exposing `CreateRequested`/`EditRequested`/`DeleteRequested` (mirrors `BucketListView`).
- `src/lazynats/KVStore/KvTab.cs`: wire the three new key-level events to dialog-open/
  confirm/mutate handlers, paralleling its existing bucket-level `OpenCreateBucketDialog`/
  `OpenEditBucketDialog`/`TryDeleteBucketAsync`.
- New `src/lazynats/KVStore/CreateKeyDialog.cs` (or similarly named): the Name+Value modal,
  shaped like `CreateBucketDialog.cs` but with a `TextView` (`TabKeyAddsTab = false`) for Value
  instead of a `TextField`.
- New small helper for the printable-text check (valid UTF-8 + no disallowed control
  characters), used by both the Edit-guard and to decode a value for seeding the edit dialog.
- `openspec/specs/nats-kv/spec.md`: requirement changes described above.
