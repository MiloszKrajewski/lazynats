## Why

The Streams tab's consumer level can create consumers (`nats-streams`' "Create Consumer") but has
no way to remove one — a consumer created by mistake, or one that's simply outlived its
subscriber, has to be deleted with the `nats` CLI or another client, the exact round-trip
lazynats exists to avoid. `nats-streams`' "Read-Only List" requirement currently rules this out
entirely at the consumer level.

## What Changes

- Add a Ctrl+D binding on the Streams tab's consumer-level list that deletes the highlighted
  consumer of the currently drilled-into stream, after a confirmation prompt naming the consumer
  — same shape as the existing stream-level "Delete Stream" (destructive, irreversible
  server-side operation; Cancel is the default, Enter-activated response).
- Confirming calls `INatsJSContext.DeleteConsumerAsync(stream, consumer)` and refreshes the
  consumer list so the deleted consumer disappears; cancelling (Esc, or choosing Cancel) leaves
  the list untouched.
- **BREAKING** (spec-level, not code): narrows `nats-streams`' "Read-Only List" requirement,
  which currently states the consumer level provides no edit-or-delete affordance at all — it
  gains delete (edit remains out of scope).

## Capabilities

### New Capabilities

(none — this extends the existing Streams tab)

### Modified Capabilities

- `nats-streams`: adds a "Delete Consumer" requirement (Ctrl+D, confirmation prompt naming the
  consumer, commit/cancel/failure behavior, highlight-moves-to-neighbor-on-delete) mirroring the
  existing "Delete Stream" requirement, and narrows "Read-Only List" so it no longer claims the
  consumer level has zero mutation affordances.

## Impact

- `Streams/ConsumerListView.cs` gains its own `Command.DeleteAll` + `Key.D.WithCtrl` binding
  (mirroring `StreamListView`'s existing `Command.New`/Ctrl+N and `Command.DeleteAll`/Ctrl+D
  layered on the shared `DrillableListView<T>` base), raising a `DeleteRequested` event.
- `Streams/StreamsTab.cs` handles `DeleteRequested`: confirm via `MessageBox.Query`, call
  `_jetStream.DeleteConsumerAsync(_currentStream, name)`, refresh the consumer list on success,
  show `MessageBox.ErrorQuery` on failure — same shape as the existing
  `TryDeleteStreamAsync`/`NeighborStreamName` pair, scoped to `_currentStream`.
- Depends on `NATS.Client.JetStream.INatsJSContext.DeleteConsumerAsync(string, string,
  CancellationToken)` (already available in the referenced package version; no new dependency).
- No new files — this is additive wiring on the two existing Streams-tab files.
