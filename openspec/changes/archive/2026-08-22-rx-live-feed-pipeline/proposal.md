## Why

The live feed pipeline (`SubscriptionRegistry` → `Channel<FeedEnvelope>` → `FeedReaderLoop` →
`MessageDeduplicator` → manual `App?.Invoke`) is hand-rolled plumbing that predates the app's
Rx adoption. `StreamDetails`/`ConsumerDetails` already compose their poll pipelines as
`Observable.Interval(...).Where(...).SelectAsync(...).ObserveOnApp(App!).Subscribe(...)`, backed
by `System.Reactive` (already a dependency, already AOT-probed via `RxProbe.cs`) and the
project's own `ObserveOnApp`/`SelectAsync` extensions in `Core/AsyncExtensions.cs`. Running two
separate concurrency idioms side by side (a raw `Channel` + a hand-rolled drain loop, alongside
Rx elsewhere) costs more in binary size and mental overhead than consolidating onto the one
already paid for. Rx composition also makes future feed transformations (per-subscription
scoping, subject filtering, tee-ing to a second consumer) a `.Where(...)` away instead of new
producer-side plumbing.

## What Changes

- Replace the shared `Channel<FeedEnvelope>` fan-in with a synchronized `Subject<FeedEnvelope>`
  (`Subject.Synchronize()`), exposed as `IObserver<FeedEnvelope>` to producers
  (`SubscriptionRegistry`) and `IObservable<FeedEnvelope>` to consumers (`LiveUpdatesView`).
- Replace `FeedReaderLoop`'s manual `WaitToReadAsync`/`TryRead` drain loop with an Rx chain:
  `.Where(dedup)` → `.Buffer(TimeSpan.FromMilliseconds(25))` → `.Where(batch.Count > 0)` →
  `.ObserveOnApp(App!)` → `.Subscribe(...)`, matching the existing poll-pipeline idiom.
- Delete `FeedReaderLoop.cs` - its responsibility fully collapses into the Rx chain above.
- **BREAKING** (internal only, no user-facing behavior change): batching becomes time-windowed
  (fixed 25ms buffer) instead of drain-whatever's-currently-queued. Net observable effect on
  screen is the same (still one main-thread dispatch per batch, batches still coalesce under
  load per the app's own render-loop timeout draining), but the latency/batching *mechanism*
  changes, which is why the `live-feed` spec's batching requirement needs updating rather than
  just the code.
- `MessageDeduplicator` is unchanged in behavior; its "single caller, no locking needed" comment
  moves from crediting `FeedReaderLoop` to crediting `Subject.Synchronize()`'s serialization
  guarantee.

## Capabilities

### New Capabilities

(none - this is a pipeline restructuring of an existing capability)

### Modified Capabilities

- `live-feed`: the "Batched Main-Thread Dispatch" requirement changes from "await once, drain
  everything currently queued, no added latency" to "buffer messages for a fixed window (25ms,
  matching the app's own render cadence) then dispatch each non-empty window as one batch."

## Impact

- **Code**: `Program.cs` (DI wiring for the shared subject), `Subscriptions/SubscriptionRegistry.cs`
  (writes via `IObserver<FeedEnvelope>.OnNext` instead of `ChannelWriter.WriteAsync`),
  `LiveFeed/FeedReaderLoop.cs` (deleted), `LiveFeed/MessageDeduplicator.cs` (comment only),
  `LiveUpdatesView.cs` (subscribes to the Rx chain in its constructor instead of starting a
  `FeedReaderLoop`), `MainWindow.cs` (resolves `IObservable<FeedEnvelope>` instead of
  `ChannelReader<FeedEnvelope>`).
- **Dependencies**: none added - `System.Reactive` is already referenced by both projects.
- **Specs**: `openspec/specs/live-feed/spec.md`'s batching requirement gets a delta.
