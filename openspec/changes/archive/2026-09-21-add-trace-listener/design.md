## Context

The Live Feed pipeline (`FeedEnvelope` → shared `Channel<FeedEnvelope>` → `FeedReaderLoop` →
`MessageDeduplicator` → `LiveLogDataSource` → `LiveUpdatesView`) already exists and is fed today
by exactly one producer shape: `SubscriptionRegistry.RunAsync`, one background task per active
NATS subscription, each calling `_sink.OnNext(new FeedEnvelope(...))` on the shared, synchronized
sink (`Subject.Synchronize(new Subject<FeedEnvelope>())`, registered in DI as both
`IObserver<FeedEnvelope>` and `IObservable<FeedEnvelope>` - see `Program.cs`).

This change adds a second producer: a `System.Diagnostics.TraceListener` that turns
`Trace.Write`/`Trace.WriteLine` calls into synthetic envelopes on the same sink, so developers get
a debug channel for free by reusing the pane, its formatting, and its dedup/batching machinery -
without a real NATS subscription or a second on-screen surface.

## Goals / Non-Goals

**Goals:**
- Any `Trace.Write`/`Trace.WriteLine` call anywhere in the app shows up as a Live Feed row, tagged
  with subject `$TRACE`, with no subscription created and no NATS traffic involved.
- Zero footprint in Release/AOT builds: the listener type and its registration are both compiled
  out under `#if DEBUG`, matching the existing `registry.Add(">")` dev-convenience block's own
  scoping and stated rationale in `Program.cs`.
- Reuse the existing envelope pipeline unchanged - no new dispatch path, no changes to
  `FeedEnvelope`, `MessageDeduplicator`, `LiveLogDataSource`, or rendering.

**Non-Goals:**
- Replacing or wrapping every `Console`/logging call site in the app - this only adds the
  listener; call sites still opt in individually via `Trace.WriteLine(...)`, same as
  `deep-trace-diagnostics`-style instrumentation elsewhere.
- A toggle/shortcut to enable or disable trace output at runtime - it's DEBUG-build-always-on,
  like the existing `registry.Add(">")` convenience.
- Preserving `TraceListener`'s indent-level/multi-write-line-buffering semantics - each
  `Write`/`WriteLine` call becomes its own row (see Decision 3).

## Decisions

### Decision 1: Build the synthetic message via `NatsMsgBuilder<byte[]>`, not `SubscriptionRegistry`
`NATS.Client.Core` (3.2.0, per `lazynats.csproj`) exposes a public `NatsMsgBuilder<T>` with
settable `Subject`/`Data`/`Headers` and a `.Msg` property that builds a `NatsMsg<T>` with no
connection or subscription involved:
```csharp
var msg = new NatsMsgBuilder<byte[]> { Subject = "$TRACE", Data = payloadBytes }.Msg;
```
This is the only way to get a `NatsMsg<byte[]>` for a `FeedEnvelope` without an actual publish or
subscribe round-trip. The listener calls `_sink.OnNext(new FeedEnvelope(DateTimeOffset.UtcNow,
TraceSubscriptionId, msg))` directly - `SubscriptionRegistry` is never involved, so its
`excludeSystem` (`$`-prefix) filtering, which only applies to messages arriving through a real
subscription, has no bearing here.

**Alternative considered**: route trace text through `SubscriptionRegistry.Add("$TRACE")` and
`connection.PublishAsync("$TRACE", ...)`. Rejected - it requires a live NATS connection for a
dev-only debug channel, round-trips through the server for no reason, and contradicts the
proposal's explicit "does not require subscription" requirement.

### Decision 2: Fixed sentinel `SubscriptionId`, not a fresh `Guid.NewGuid()` per envelope
`FeedEnvelope.SubscriptionId` exists for `MessageDeduplicator`'s duplicate/canonical bookkeeping
and is never used to look up an actual `Subscription` elsewhere (confirmed - the only other reader
is `MessageDeduplicator`). Its suppression rule only fires when an incoming envelope's
`SubscriptionId` *differs* from the stored canonical one for the same key
(`envelope.SubscriptionId != canonical.SubscriptionId` - see `MessageDeduplicator.IsDuplicate`).
The listener uses one fixed, well-known value, `TraceFeedListener.SubscriptionId` (`Guid.Empty`),
for every envelope it produces. Because every trace envelope shares that same id, two
identical-text trace calls are never judged as "the same message arriving via a different
source" and so are never suppressed - every distinct `Trace` call reliably produces its own row,
which is what a debug channel needs. `Guid.Empty` is also self-documenting - "this didn't come
from a real subscription" - at a glance in a debugger.

**Alternative considered**: `Guid.NewGuid()` per envelope. Rejected - it would make two
identical-text trace calls fired within the dedup window look like the *same* message arriving
via two different sources, and `MessageDeduplicator` would silently collapse the second one -
losing genuine repeated debug output, which is the opposite of what a trace channel should do.

### Decision 3: One envelope per `Write`/`WriteLine` call, no cross-call line buffering
`TraceListener` is designed for streaming text output (`Write` appends without a newline,
`WriteLine` appends one) and its own indent-handling machinery. The Live Feed is a row-per-message
view, not a text stream, so reproducing exact stream semantics (buffering `Write` fragments until
a trailing `WriteLine`) would add complexity for no payoff developers actually want: every
`Trace.WriteLine(...)` call is already a complete, meaningful unit. The listener overrides both
`Write(string?)` and `WriteLine(string?)` and pushes one envelope per call, using the call's own
`message` as the payload text (UTF-8 encoded) - both calls null- and empty-string-guarded so
`TraceListener`'s incidental empty writes (e.g. from indent handling elsewhere in the base class)
don't produce blank rows.

**Alternative considered**: buffer `Write` fragments and flush on `WriteLine`, mimicking a real
text stream. Rejected per above - `deep-trace-diagnostics`-style call sites in this codebase use
`Trace.WriteLine` for complete messages, not `Trace.Write` fragments, so the simpler 1:1 mapping
is both correct today and easier to reason about.

### Decision 4: Construct and register the listener in `Program.cs`, not via DI
`TraceFeedListener` needs the same `feed` (`Subject.Synchronize(new Subject<FeedEnvelope>())`)
instance already constructed in `Program.cs` before DI registration happens. The existing
`#if DEBUG registry.Add(">") #endif` dev-convenience block already sits right after `feed` and
`registry` are constructed and before `ServiceCollection` setup - this change adds
`Trace.Listeners.Add(new TraceFeedListener(feed))` to that same block, keeping all "DEBUG-only
feed wiring" in one place instead of introducing a DI-resolved path for a type nothing else needs
to look up.

## Risks / Trade-offs

- **`Trace.Write`/`WriteLine` can be called from any thread, including concurrently** →
  Mitigated: the shared `feed` sink is already `Subject.Synchronize`-wrapped specifically to make
  concurrent `OnNext` calls safe (see `Program.cs`'s comment on that line and
  `SubscriptionRegistry`'s own multi-task-writer pattern) - `TraceFeedListener` needs no
  additional locking.
- **A very verbose trace call site could flood the feed** → Accepted as a dev-only, opt-in-per-
  call-site concern (Non-Goal): the feed's existing ring-buffer cap and batched dispatch already
  bound the cost of high message volume, same as a chatty real subscription would.

## Migration Plan

None - purely additive, DEBUG-only. No data model, spec, or Release-build change.
