## 1. TraceFeedListener

- [x] 1.1 Add `src/lazynats/LiveFeed/TraceFeedListener.cs`, entire file body wrapped in
      `#if DEBUG` / `#endif`, defining `internal sealed class TraceFeedListener : TraceListener`.
- [x] 1.2 Constructor takes `IObserver<FeedEnvelope> sink` and stores it.
- [x] 1.3 Define a fixed sentinel `internal static readonly Guid SubscriptionId = Guid.Empty;` and
      a `private const string TraceSubject = "$TRACE";`.
- [x] 1.4 Implement a private `Emit(string? message)` helper: no-ops on `null`/empty `message`;
      otherwise builds `new NatsMsgBuilder<byte[]> { Subject = TraceSubject, Data =
      Encoding.UTF8.GetBytes(message) }.Msg` and calls `sink.OnNext(new
      FeedEnvelope(DateTimeOffset.UtcNow, SubscriptionId, msg))`.
- [x] 1.5 Override `Write(string? message)` and `WriteLine(string? message)`, each calling
      `Emit(message)` - one envelope per call, no cross-call buffering (see design.md Decision 3).

## 2. Wiring

- [x] 2.1 In `Program.cs`'s existing `#if DEBUG` dev-convenience block (where `registry.Add(">")`
      lives), add `Trace.Listeners.Add(new TraceFeedListener(feed));` after `feed` is constructed.
- [x] 2.2 Add the `System.Diagnostics` using directive if not already present.

## 3. Verification

- [x] 3.1 `dotnet build src/lazynats.sln` in both Debug and Release configurations - confirm the
      Release build compiles with `TraceFeedListener` and its registration absent (e.g. grep the
      Release output/IL for the type, or temporarily add a call site and confirm it's a no-op
      outside DEBUG).
- [x] 3.2 Run the app (Debug config) against a local NATS server; from a running session, trigger
      a code path that calls `Trace.WriteLine(...)` (or add a temporary one) and confirm a
      `$TRACE`-subject row appears in the Live Feed with the expected payload text, without any
      entry appearing in the Subscribe tab's active-subscriptions list.
- [x] 3.3 Confirm two identical `Trace.WriteLine` calls fired back-to-back both appear as separate
      rows (not collapsed by dedup), matching design.md Decision 2 / the spec's dedup scenario.
