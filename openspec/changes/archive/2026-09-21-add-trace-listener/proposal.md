## Why

`lazynats` is a TUI: it owns the whole screen, so there's no `Console.WriteLine` a developer can
reach for while debugging internals, and most of the app's interesting state (subscription
lifecycle, dedup decisions, poll timing) has no DI seam to inject a logger into. The Live Feed
pane already renders a scrollable, timestamped, payload-classified stream of messages - which is
exactly the shape ad-hoc debug output wants. Piping `System.Diagnostics.Trace` calls into that
pane as fake `$TRACE`-subject messages gives developers a debug channel that's already built,
without adding a second on-screen surface or touching the real NATS traffic.

## What Changes

- Add a `TraceFeedListener : TraceListener` that turns `Trace.Write`/`Trace.WriteLine` calls into
  `FeedEnvelope`s carrying a synthetic `NatsMsg<byte[]>` on subject `$TRACE`, pushed directly to
  the shared feed sink (`IObserver<FeedEnvelope>`) - bypassing `SubscriptionRegistry` entirely, so
  no subscription is created or required.
- Register the listener in `Program.cs`, wrapped in `#if DEBUG` so it (and the listener type
  itself) is entirely absent from Release/AOT builds.

## Capabilities

### New Capabilities
- `trace-listener`: a DEBUG-only `TraceListener` that forwards `System.Diagnostics.Trace` output
  into the Live Feed as synthetic `$TRACE`-subject messages, independent of any real subscription.

### Modified Capabilities
(none - `live-feed`'s `FeedEnvelope`/dispatch pipeline is reused unchanged, not altered)

## Impact

- New file(s) under `src/lazynats/LiveFeed/` (or a new `Diagnostics/` folder) for
  `TraceFeedListener`, compiled only under `DEBUG`.
- `Program.cs`: register the listener with `Trace.Listeners` inside the existing `#if DEBUG` dev-
  convenience block, after `feed`/`registry` are constructed.
- No change to `FeedEnvelope`, `SubscriptionRegistry`, dedup, or rendering - synthetic envelopes
  flow through the exact same pipeline as real ones.
