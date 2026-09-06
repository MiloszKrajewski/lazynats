## Why

The Streams tab can create streams (`nats-streams`' "Create Stream") but has no way to remove
one — a stream created by mistake, or one that's simply run its course, has to be deleted with
the `nats` CLI or another client, which is exactly the round-trip lazynats exists to avoid.

## What Changes

- Add a Ctrl+D binding on the Streams tab's stream-level list that deletes the highlighted
  stream, after a confirmation prompt naming the stream (destructive, irreversible server-side
  operation — unlike the existing silent Ctrl+D precedent in `ListEditorView<T>`, which only ever
  deletes cheap local rows).
- Confirming calls `INatsJSContext.DeleteStreamAsync` and refreshes the stream list so the
  deleted stream disappears; cancelling (Esc, or choosing Cancel) leaves the list untouched.
- **BREAKING** (spec-level, not code): supersedes `nats-streams`' "Read-Only List" requirement at
  the stream level — the consumer level remains fully read-only; only stream-level delete is
  added (consumer delete stays out of scope, per the doc's own "baseline operations" split).

## Capabilities

### New Capabilities

(none — this extends the existing Streams tab)

### Modified Capabilities

- `nats-streams`: adds a "Delete Stream" requirement (Ctrl+D, confirmation prompt, commit/cancel/
  failure behavior) and narrows "Read-Only List" so it no longer claims the stream level has zero
  mutation affordances — the consumer level's read-only guarantee is unchanged.

## Impact

- `Streams/StreamListView.cs` gains its own `Command.DeleteAll` + `Key.D.WithCtrl` binding
  (mirroring how it already layers `Command.New` + Ctrl+N on top of the shared
  `DrillableListView<T>` base), raising a `DeleteRequested` event.
- `Streams/StreamsTab.cs` handles `DeleteRequested`: confirm via `MessageBox.Query`, call
  `_jetStream.DeleteStreamAsync`, refresh the list on success, show `MessageBox.ErrorQuery` on
  failure — same shape as its existing `OpenCreateStreamDialog`/`TryCreateStreamAsync` pair.
- Depends on `NATS.Client.JetStream.INatsJSContext.DeleteStreamAsync(string, CancellationToken)`
  (already available in the referenced package version; no new dependency).
- No new files — this is additive wiring on the two existing Streams-tab files.
