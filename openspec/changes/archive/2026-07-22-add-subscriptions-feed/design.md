## Context

`lazynats.App` currently has a connected `NatsConnection` singleton (`Program.cs`) that is never used beyond the connect call, and a demo feed (`heartbeat`, an `Observable.Interval` producing fake strings) wired through `LiveUpdatesView`/`LiveLogDataSource`, which render flat strings with no structure. There is no subscription management and no real message ever reaches the feed.

## Goals / Non-Goals

**Goals:**
- A Subscriptions screen: add/delete subject-pattern subscriptions (e.g. `invoices.>`).
- A single, global (not per-tab-scoped) live feed fed by all currently active subscriptions.
- A merge point — `Channel<NatsMsg<byte[]>>` — so the feed doesn't care how many subscriptions are active or which one a message came from.
- Correct, low-overhead dispatch to the UI thread under bursty NATS throughput.
- A practical answer to the duplicate-message problem caused by overlapping subscription patterns.

**Non-Goals:**
- Message composer/sending (next slice).
- Streams / Consumers / KV / OBJ tabs (JetStream-backed; out of scope here).
- Message templates.
- Queue-group subscriptions (load-balanced delivery) — this is a monitoring tool; fan-out, not load-balancing, is the default assumption.
- A real "edit subscription" action — delete+add is equivalent as long as a subscription row is just a pattern string (see Open Questions).
- Pattern-match tagging as the dedup mechanism — recorded below as a considered alternative, not built now.

## Decisions

**Global feed, not per-tab-scoped.**
Selecting a subscription (or any other management-tab item) does not filter the feed. The feed always shows everything currently subscribed to, across all active patterns. This keeps the feed a simple shared singleton rather than per-selection state; scoping/filtering can be layered on top later (e.g. a client-side filter box) without changing the pipeline.

**Message envelope: `FeedEnvelope` wraps the raw `NatsMsg<byte[]>`.**
```csharp
internal sealed record FeedEnvelope(DateTimeOffset ReceivedAt, Guid SubscriptionId, NatsMsg<byte[]> Message);
```
`ReceivedAt` is stamped by the background task at the moment it dequeues the message from `SubscribeAsync` — since core NATS has no server timestamp (see Dedup below), this client-side receipt time is the only one that will ever exist, so it's captured once, early, and carried with the message instead of being recomputed or left implicit at each later consumer (reader loop, dedup, feed rendering). `SubscriptionId` is a `Guid` assigned once when a subscription is added (see `nats-subscriptions` spec); it identifies *which* active subscription produced this particular delivery, distinct from the subject pattern string itself. Wrapping now — rather than passing raw `NatsMsg<byte[]>` around and bolting metadata on piecemeal later — avoids reworking the channel type, the reader loop, and the dedup key source once these facts turn out to be needed (they already are, immediately, by dedup and by the feed's timestamp column from `doc/UI.md`).

**One `Channel<FeedEnvelope>`, N background subscription tasks.**
Each active subscription pattern runs its own background task iterating `connection.SubscribeAsync<byte[]>(pattern)`, wrapping each received message in a `FeedEnvelope` (stamping `ReceivedAt` and its own `SubscriptionId`), and writing the envelope into the shared channel. Adding a subscription starts a new task; deleting one cancels its task and disposes the subscription. The channel is unbounded: `NATS.Client.Core` already applies its own per-subscription slow-consumer limits inside `SubscribeAsync`, so this merge channel isn't the first or only backpressure point — adding bounded-channel backpressure here would just duplicate that without adding safety.

**Reader loop: await once, drain via `TryRead`, one `App.Invoke` per batch.**
```
while (await reader.WaitToReadAsync())
{
    var batch = new List<FeedEnvelope>();
    while (reader.TryRead(out var envelope)) batch.Add(envelope);
    if (batch.Count > 0)
        app.Invoke(() => feed.AddRange(batch));
}
```
This bounds UI-thread marshaling to once per burst rather than once per message — necessary since NATS can produce messages far faster than the terminal can usefully redraw, and `App.Invoke` round-trips through the main loop.

**Dedup: `hash(subject + headers + payload)`, collapsed within a short trailing time window.**
The only realistic source of duplicate rows in a single global feed is the same physical message matching more than one active subscription pattern (e.g. `invoices.>` and `invoices.get.*` both active; the server delivers one copy per matching subscription). Core NATS carries **no server timestamp or sequence number** on the wire for plain pub/sub — that metadata (`Metadata().Timestamp`, stream sequence) only exists on JetStream consumer messages (`NatsJSMsg<T>`), a different, out-of-scope tab. The only timestamp available is `FeedEnvelope.ReceivedAt`, client-stamped at receipt, and the two duplicate deliveries are two distinct receive events with two different `ReceivedAt` values — so it is deliberately **excluded** from the hash; including it would silently defeat the dedup (the hashes would never match). `SubscriptionId` is excluded from the hash for the same reason, inverted: it's expected to *differ* between the two duplicate envelopes (that's precisely what makes them duplicates rather than the same envelope read twice), so including it would prevent the match instead of enabling it. The hash is computed from `FeedEnvelope.Message` (subject, headers, payload) only.

Carrying `SubscriptionId` on every envelope is a small bonus this design gets for free: when a duplicate is suppressed, the surviving row's contributing subscriptions are already known (the kept envelope's `SubscriptionId` plus the suppressed one's), so a later enhancement — showing "matched by: X, Y" instead of just silently dropping the duplicate — would not require touching the channel or envelope shape again, only the dedup cache and rendering. Not built now; noted as a low-cost future option alongside the pattern-match-tagging alternative below.

**Alternative considered, not built now: pattern-match tagging.**
Instead of hashing, client-side test each incoming subject against all currently active subscription patterns and tag the row with which pattern(s) matched (a small subject matcher over literal/`*`/`>` tokens). This is more precise than content hashing — it doesn't risk collapsing two legitimately identical-looking messages (e.g. a repeated heartbeat payload arriving twice within the window) — and turns "why did I only see this once" into visible information ("matched by: X, Y") rather than a silent merge. Deferred because it's more code for a problem the simpler hash approach may already solve well enough in practice; revisit if the hash heuristic proves too lossy.

**Subscriptions are pattern strings only; no separate edit path.**
A subscription row is just the subject pattern. Changing it is: cancel the old background task/subscription, start a new one with the new pattern — the same mechanics as delete+add, so no distinct "edit" code path is built.

## Risks / Trade-offs

- **[Hash collisions]** Two genuinely different events that happen to produce identical `subject+headers+payload` within the dedup window (e.g. a repeating heartbeat with a static payload) will be wrongly collapsed into one row. → Mitigation: none yet; window size is a starting guess, not measured against real traffic. Revisit with pattern-match tagging (above) if this proves disruptive.
- **[Window size is a guess]** No production traffic has been measured yet to size the trailing window correctly. → Mitigation: keep the window small and tune once real subscription overlap is exercised.
- **[Unbounded channel]** If a subscription task falls far behind (e.g. UI thread stalls), the channel can grow unbounded in memory. → Accepted: `NATS.Client.Core`'s own slow-consumer limits bound how much any single subscription can queue before the client itself drops/errors, so this is a secondary, already-mitigated concern, not a new one introduced by this design.

## Migration Plan

Net-new behavior inside `lazynats.App`; nothing else is affected. Rollout: remove the `heartbeat` demo wiring, add the subscription registry + channel pipeline, adapt `LiveUpdatesView`/`LiveLogDataSource` to render structured `FeedEnvelope` rows (timestamp, subject, headers, payload) instead of flat strings, add the Subscriptions screen. No rollback concerns beyond reverting the change — no persisted state or external migration involved.

## Open Questions

- Does a subscription row ever need state beyond the pattern itself (queue group, display color, pause/mute)? Nothing in scope today needs it; deferred until a concrete need appears — if one does, it would also be the point to reconsider a real "edit" action.
- Dedup window size is unset/unmeasured — to be tuned once this is running against real subscription traffic.
