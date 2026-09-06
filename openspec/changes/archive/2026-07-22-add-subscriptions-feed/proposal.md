## Why

`lazynats` currently has only a skeleton UI: `MainWindow` wires a fake `heartbeat` observable into `LiveUpdatesView`, and there is no way to subscribe to a real NATS subject or see actual messages arrive. Per exploration of `doc/UI.md`, the Subscriptions screen (live NATS monitoring) plus the global feed it drives is the highest-ROI slice to build first — it's the foundation the message-composer/sending work (planned next) needs something real to send into and observe.

## What Changes

- Add a Subscriptions screen: a list of subject patterns (e.g. `invoices.>`), addable and deletable. No separate edit action — changing a pattern is delete-then-add.
- Each active subscription pattern runs its own background reader over `NatsConnection.SubscribeAsync<byte[]>`, funneling received messages into one shared `Channel<NatsMsg<byte[]>>` so the feed has a single merge point regardless of how many subscriptions are active.
- Replace the fake `heartbeat` demo feed with a real feed driven by that channel: a reader loop awaits once, then drains everything currently available without waiting, and dispatches each drained batch to the UI thread with a single `App.Invoke` call.
- Add dedup for messages that match more than one active subscription pattern (the server delivers one copy per matching subscription, so overlapping patterns like `invoices.>` and `invoices.get.*` produce visible duplicates): key on `hash(subject + headers + payload)` and collapse repeats seen again within a short trailing time window. (Core NATS carries no server timestamp/sequence, so timestamp is deliberately excluded from the hash — see design.md.)

Out of scope for this change (later slices):
- Message composer / sending.
- Message templates.
- Streams / Consumers / KV / OBJ tabs.
- Pattern-match tagging as a dedup alternative (recorded in design.md, not built).

## Capabilities

### New Capabilities
- `nats-subscriptions`: managing a list of live subject-pattern subscriptions (add/delete), each backed by a real NATS subscription.
- `live-feed`: the merged, deduplicated, batched message pipeline from active subscriptions into the feed view.

### Modified Capabilities
- None. The existing `heartbeat`/`LiveUpdatesView` wiring is throwaway scaffold code, not a documented capability.

## Impact

- `lazynats.App`: `Program.cs`/`Services.cs` (remove `heartbeat` demo, wire real subscription registry), `MainWindow.cs` (add Subscriptions screen), `LiveUpdatesView.cs`/`LiveLogDataSource.cs` (adapt from flat strings to structured `FeedEnvelope` rows — see design.md).
- No new package dependency: `NATS.Client.Core` (for `NatsConnection`/`NatsMsg<byte[]>`) is already available transitively via `NATS.Client.JetStream`, already referenced in `lazynats.App.csproj`, and a connected singleton `NatsConnection` already exists in `Program.cs`.
