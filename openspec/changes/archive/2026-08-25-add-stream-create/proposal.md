## Why

The Streams tab is currently read-only by design (`nats-streams`' "Read-Only List" requirement)
— streams have to be created with the `nats` CLI or another client before they show up. That's a
gap for a tool whose whole point is managing JetStream without leaving the terminal. Adding
in-app stream creation removes the CLI round-trip for the most common setup step.

## What Changes

- Add a Ctrl+N binding on the Streams tab's stream-level list that opens a "New Stream" modal
  dialog.
- The dialog collects the minimal set of fields needed to create a usable stream: Name, Subjects
  (space/comma/semicolon-separated), Retention policy, and Max Age. Everything else in
  `StreamConfig` (storage backend, limits beyond max age, mirrors/sources, replicas, ...) is left
  at server defaults for this slice and deferred to a future "Advanced" section of the dialog.
- On commit, the dialog calls `INatsJSContext.CreateStreamAsync` and the stream list is refreshed
  to include the new stream, which becomes highlighted.
- **BREAKING** (spec-level, not code): supersedes `nats-streams`' "Read-Only List" requirement at
  the stream level — the consumer level remains read-only; only stream creation is added.

## Capabilities

### New Capabilities

(none — this extends the existing Streams tab rather than introducing a new management surface)

### Modified Capabilities

- `nats-streams`: adds a "Create Stream" requirement (Ctrl+N, dialog fields, validation, commit
  behavior) and narrows "Read-Only List" so it no longer claims the stream level has zero mutation
  affordances — the consumer level's read-only guarantee is unchanged.

## Impact

- New `Streams/NewStreamOptions.cs`: a dialog-owned result record (nullable fields for "unset",
  e.g. `TimeSpan? MaxAge`), kept deliberately separate from `NATS.Client.JetStream.Models.StreamConfig`'s
  wire representation (which uses inconsistent per-field sentinels like `-1` for "unlimited"), plus
  a `ToStreamConfig()` translation used only at the `CreateStreamAsync` call site.
- New `Streams/CreateStreamDialog.cs` (multi-field `Dialog<NewStreamOptions>`, the first modal in
  the codebase with more than one field — `PatternDialog`/`HeaderDialog` are both single-`TextField`
  precedent but don't cover multi-field layout, per-field validation, or `Cancel`/`Create` button
  wiring).
- `Streams/StreamListView.cs` gains its own Ctrl+N command (mirroring how `ConsumerListView`
  already layers its own Esc/Backspace on top of the shared `DrillableListView<T>` base, which
  otherwise only wires Ctrl+R).
- `Streams/StreamsTab.cs` owns running the dialog, calling `CreateStreamAsync`, and refreshing
  `StreamListView` with the new stream highlighted.
- Depends on `NATS.Client.JetStream.INatsJSContext.CreateStreamAsync(StreamConfig, CancellationToken)`
  (already referenced elsewhere in the project; no new package).
