## Context

The live feed currently flows through a hand-rolled pipeline:

```
N subscription tasks ──WriteAsync──► Channel<FeedEnvelope> (unbounded, singleton)
                                              │
                                    FeedReaderLoop.RunAsync
                                    await WaitToReadAsync, then TryRead-loop:
                                    drain everything queued *right now*,
                                    filtering via dedup.IsDuplicate() inline
                                              │
                                       onBatch(batch)
                                              │
                              App?.Invoke(() => foreach env in batch: OnEvent(env))
                                              ▼
                                    ObservableCollection → ListView
```

Meanwhile `StreamDetails`/`ConsumerDetails` already run their poll pipelines as Rx chains:
`Observable.Interval(PollInterval).Where(...).SelectAsync(...).ObserveOnApp(App!).Subscribe(...)`,
backed by `System.Reactive` (already referenced by both `lazynats` and `lazynats.AotProbe`, and
validated there via `RxProbe.cs`) and the project's own `Core/AsyncExtensions.cs`
(`SelectAsync`, `ObserveOnApp`). The live feed is the one remaining pipeline in the app using a
different idiom.

`SubscriptionRegistry.RunAsync` runs one background task per active subscription, each writing
into the same shared sink concurrently - whatever replaces `Channel` must preserve that
many-writers-one-sink safety.

`openspec/specs/live-feed/spec.md`'s "Batched Main-Thread Dispatch" requirement currently
specifies drain-now batching (no added latency, batch boundaries follow arrival). This changes
under the new design - see Decisions.

## Goals / Non-Goals

**Goals:**
- One consistent idiom (Rx) for every data-to-UI pipeline in the app: poll-based (streams,
  consumers) and push-based (live feed) alike.
- Preserve existing observable behavior: single global deduplicated feed, one main-thread
  dispatch per batch, no per-message UI-thread marshaling.
- Preserve the "no locking needed" simplicity of `MessageDeduplicator` - it must still only ever
  be called from one logical place at a time.
- No new dependency - `System.Reactive` is already paid for.
- Make future feed transformations (per-subscription scoping, subject filtering, tee to a second
  consumer) composable as `.Where(...)`/additional `.Subscribe(...)` calls rather than requiring
  new producer-side plumbing.

**Non-Goals:**
- Real backpressure on inbound NATS messages. Today's `Channel` is unbounded (no backpressure in
  practice); the `Subject`-based replacement is equally unbounded. Introducing actual
  backpressure (bounded channel with drop/wait semantics) is out of scope here.
- Scoping the feed to a selected subscription, subject filtering, or any other new
  user-facing feed capability. This change only restructures the pipeline; it does not add
  features the composability enables.
- Changing `MessageDeduplicator`'s dedup key or window semantics.

## Decisions

### Fan-in: `Subject<FeedEnvelope>` wrapped in `Subject.Synchronize()`, not a raw `Subject`

`SubscriptionRegistry` runs one task per subscription, all calling into the shared sink
concurrently. A raw `Subject<T>.OnNext` is not safe under concurrent calls from multiple
threads - notifications can interleave. `Subject.Synchronize()` serializes all `OnNext` calls,
restoring the same safety guarantee `Channel` gave for free, and - just as importantly -
guarantees `MessageDeduplicator.IsDuplicate` (downstream of the `Where`) is still only ever
invoked one call at a time, preserving its existing "no locking needed" invariant.

**Alternative considered**: manual lock around a raw `Subject`. Rejected - `Synchronize()` is
the built-in, idiomatic way to get this and needs no new code.

Registered in DI as the same singleton instance under two narrower types: `IObserver<FeedEnvelope>`
for `SubscriptionRegistry` (producer - can only push) and `IObservable<FeedEnvelope>` for
`LiveUpdatesView` (consumer - can only subscribe). Neither side can accidentally reach the raw
unsynchronized `Subject` or call the other side's members.

### Batching: `Buffer(TimeSpan.FromMilliseconds(25))`, not a literal port of drain-now

`Channel`'s drain-now batching (await once, then synchronously drain whatever's queued) has no
direct Rx equivalent - it's a pull-based "drain a real buffer" behavior, and `Subject`/`Observable`
is pure push with nothing to drain. `Buffer(TimeSpan)` is the closest idiomatic replacement, but
it changes the batching *mechanism*: from arrival-driven (batch boundary = "whatever showed up
since the reader last woke") to clock-driven (batch boundary = fixed time window).

The window is 25ms, deliberately a bit coarser than the app's own render cadence
(`Application.MaximumIterationsPerSecond = 60`, i.e. ~16.67ms/iteration):

- **Tighter than render cadence buys nothing.** `IApplication.Invoke` queues via
  `AddTimeout(TimeSpan.Zero, ...)` and `ITimedEvents.RunTimers` runs *all* due timeouts per
  iteration before a single layout/draw pass. So batches finer than one render iteration just
  collapse into the same draw anyway - pure overhead (extra `List` + closure + timeout-queue
  entries) for zero additional visible frames.
- **Coarser adds real latency.** For a live feed whose entire purpose is watching messages
  arrive close to real time, added display latency is a real UX cost; the CPU/allocation
  savings from batching coarser are marginal (the dominant cost is the per-envelope `OnEvent`
  work, which happens either way, not the batch envelope itself).
- **Exactly matching the render cadence (16.67ms) risks phase-drift noise.** `Buffer`'s timer
  and the main loop's iteration timer are two independent, unsynchronized clocks. Two clocks at
  the same nominal period tend to drift in and out of phase, producing an uneven pattern (one
  batch per iteration, then a stretch of zero followed by two). Deliberately picking a period a
  bit longer (25ms) means most iterations see exactly one batch waiting, occasionally none,
  rarely a pileup - at the cost of ~8ms of added latency nobody will perceive.

`.Buffer(TimeSpan)` can emit empty lists for windows with no traffic; `.Where(batch => batch.Count > 0)`
filters those before `ObserveOnApp`, preserving today's "batch is never empty" behavior.

**Alternative considered**: `Buffer(count, TimeSpan)` (emit on whichever comes first, N items or
the timeout) to cap worst-case batch size under a flood. Rejected for this change - `_events`
already caps display at 128 rows per `OnEvent`, so an oversized batch just means more trimming
work within one dispatch, not unbounded growth. Worth revisiting if profiling ever shows it
matters.

### UI marshaling: reuse `ObserveOnApp` unchanged

No new work - `Core/AsyncExtensions.cs`'s existing `ObserveOnApp` extension already does exactly
what `LiveUpdatesView.OnBatch`'s manual `App?.Invoke(...)` did, and is already proven by the
poll pipelines.

### `LiveUpdatesView` defers subscribing until its `Initialized` event fires

**Revised during implementation** - the original version of this decision (see git history)
argued `App` was already live at `LiveUpdatesView`'s construction time, citing `MainWindow.cs`
calling `MessageBox.Query(App!, ...)` in its own constructor right after constructing
`LiveUpdatesView`. That reasoning was wrong: that `MessageBox.Query` call is inside an
`ItemSelected +=` event-handler lambda, evaluated only when the event later fires, not at
construction time. Wiring the Rx chain directly in the constructor crashed with a
`NullReferenceException` on the first live message - `View.App` resolves by walking up the
`SuperView` chain, and `LiveUpdatesView` has no `SuperView` yet at construction (it's added to
`feedFrame`, which is added to `MainWindow`, afterward), so `App` was null and the captured
`App!` closure threw once actually invoked.

Fix: subscribe inside a handler for the view's `Initialized` event (`ISupportInitializeNotification`,
raised once, after the view is attached and the app is running) instead of in the constructor
body. Same effect as `StreamDetails`/`ConsumerDetails`'s `SetActive`-gated deferral, different
mechanism - `LiveUpdatesView` has no natural "activate" trigger (it's always live, not tied to
tab focus), so it defers to the framework's own initialization lifecycle event rather than an
externally-driven flag.

### Lifecycle: `IDisposable` subscription replaces `CancellationTokenSource`

`LiveUpdatesView._cts` (cancel + dispose) is replaced by the `IDisposable` returned from
`.Subscribe(...)`, disposed in `Dispose(bool)`. Matches the `_subscription` field pattern already
used in `StreamDetails`/`ConsumerDetails`.

## Risks / Trade-offs

- **[Risk]** Sustained flood where message production outpaces the render loop: every 25ms tick
  allocates a new `List<FeedEnvelope>` + closure + `AddTimeout` entry regardless of whether the
  previous one has been consumed, which is a worse allocation profile under sustained overload
  than `Channel`'s single queue (O(1) per envelope, no per-tick allocation).
  → **Mitigation**: none needed now - `_events` already caps at 128 visible rows either way, and
  this only matters under sustained, extreme overload. Revisit with `Buffer(count, TimeSpan)` if
  it ever shows up in practice.
- **[Risk]** `Subject.Synchronize()` misuse - if a future change reaches for the raw `Subject`
  instead of the synchronized wrapper (e.g. a new producer registered against the wrong DI type),
  the thread-safety guarantee silently disappears.
  → **Mitigation**: only ever register the synchronized instance in DI, under the narrow
  `IObserver<FeedEnvelope>`/`IObservable<FeedEnvelope>` types - never register the concrete
  `Subject<FeedEnvelope>` itself, so there's no wrong type to reach for.
- **[Trade-off]** Time-windowed batching is a real behavior change vs. the documented drain-now
  requirement, not just an implementation swap - hence the `live-feed` spec-delta rather than a
  silent code change.

## Migration Plan

Single-PR, no data migration or rollback concerns (in-process pipeline only, no persisted
state). Sequence: swap DI registration → update `SubscriptionRegistry` → replace
`LiveUpdatesView`'s subscription wiring → delete `FeedReaderLoop.cs` → update
`MessageDeduplicator`'s comment → update `MainWindow.cs`'s service resolution → update the
`live-feed` spec.

## Open Questions

None outstanding - all prior open questions (buffer window value, thread-safety approach,
constructor timing) were resolved during design exploration.
