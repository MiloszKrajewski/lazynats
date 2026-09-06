## Context

`MessageDeduplicator` (`src/lazynats/LiveFeed/MessageDeduplicator.cs`) computes a dedup key per
received `FeedEnvelope` to collapse the same physical message when it matches more than one
active overlapping subscription pattern (see `openspec/specs/live-feed/spec.md` and the
archived `add-subscriptions-feed` design's "Dedup" decision). The class is documented as
single-threaded — only ever called from the one `FeedReaderLoop` that owns it — so no locking
exists or is needed around `_lastSeen`.

Today `ComputeKey` uses `System.HashCode`: `hash.Add(subject)`, `hash.Add(headerKey)` /
`hash.Add(headerValue.ToString())` per header, and a `foreach (var b in data) hash.Add(b)` loop
over the raw payload — one call per byte, not vectorized, appended into a 32-bit accumulator.
This is on the hot path (once per received NATS message) and its collision risk at 32 bits is
already an accepted, unmitigated risk noted in the prior design.

## Goals / Non-Goals

**Goals:**
- Replace the per-byte `HashCode` loop with a bulk, span-based hash over the payload.
- Widen the dedup key from 32 to 64 bits to shrink (not eliminate) accepted collision risk.
- Avoid unnecessary intermediate allocations (UTF-8 transcoding, `StringValues.ToString()`
  joins) while computing the key.
- Keep `lazynats.AotProbe` coverage current per its existing convention: any new package gets
  its own probe before the main app relies on it under `PublishAot`.

**Non-Goals:**
- No change to dedup *semantics*: same fields (subject + headers + payload), same trailing
  window, same exclusion of `ReceivedAt`/`SubscriptionId`.
- Not attempting to eliminate hash collisions entirely (still a probabilistic key, just a wider
  one) — pattern-match tagging remains the documented fallback if collisions prove disruptive
  in practice.
- Not introducing field-boundary delimiters between subject/header/payload appends. Plain
  concatenation is a narrower collision surface than the byte-identity collisions the old
  32-bit hash already accepted, and the fields' natural shapes (subject is a dot-separated
  token string, payload is arbitrary bytes) make an accidental boundary collision much less
  likely in practice than an outright accepted risk already on the books. Revisit only if this
  proves wrong.

## Decisions

**Hash algorithm: `System.IO.Hashing.XxHash3`, one new instance per `ComputeKey` call.**
`XxHash3` is 64-bit, non-cryptographic, SIMD-accelerated, and exposes an incremental
`Append(ReadOnlySpan<byte>)` / `GetCurrentHashAsUInt64()` API well suited to combining several
discontiguous inputs (subject, headers, payload) without concatenating them into one buffer
first. A fresh instance is created per call rather than reusing one hasher field with `Reset()`
between calls: `XxHash3` is a reference type, so reuse would save one small Gen0 allocation per
message, but at the cost of a lifecycle hazard (any exception between `Append` and
`GetCurrentHashAsUInt64` would leave the instance dirty for the next message, silently
corrupting the next key). That allocation is negligible next to the per-byte loop it replaces,
so correctness-by-construction wins.

**String content: `MemoryMarshal.AsBytes` over the UTF-16 char span, not `Encoding.UTF8.GetBytes`.**
The hash key only needs to be internally consistent (same string → same bytes), not a real
UTF-8 encoding — nothing decodes these bytes back into text. `MemoryMarshal.AsBytes<char>` is a
zero-copy reinterpret cast over the string's existing UTF-16 storage, so it skips the
transcoding pass `Encoding.UTF8.GetBytes` would otherwise do on every subject and header value.

**Headers: iterate `StringValues` directly instead of calling `.ToString()`.**
`NatsHeaders` values are `Microsoft.Extensions.Primitives.StringValues`. The current code calls
`headerValue.ToString()`, which allocates a new joined string when a header has more than one
value. `StringValues` is itself `IEnumerable<string?>`; appending each contained string
individually to the hasher avoids that join allocation entirely.

**Dedup key type: `Dictionary<int, DateTimeOffset>` → `Dictionary<ulong, DateTimeOffset>`.**
Follows directly from the 64-bit hash. No other change to `IsDuplicate`/`Prune`'s logic.

**New dependency gets an AOT probe.**
`System.IO.Hashing` is a new package reference for `lazynats`. Per the `aot-probe` spec's
"Isolated Per-Library Probes" requirement, add `Probes/XxHash3Probe.cs` to
`lazynats.AotProbe` exercising `Append`/`GetCurrentHashAsUInt64` (not just construction) and
register it in that project's `Program.cs`, alongside the existing NATS/Rx/DI probes.

**Alternative considered: reused hasher instance field.**
Rejected in favor of a fresh instance per call — see the allocation-vs-lifecycle-safety
rationale above.

**Alternative considered: `XxHash3.HashToUInt64` one-shot static API.**
Rejected because it needs one contiguous `ReadOnlySpan<byte>` up front, which would require
concatenating subject + headers + payload into a single buffer before hashing — reintroducing
an allocation/copy step the incremental `Append` API avoids.

## Risks / Trade-offs

- **[Still a probabilistic key]** 64 bits shrinks collision probability drastically versus 32
  bits but doesn't eliminate it. → Accepted, consistent with the prior design's stance;
  pattern-match tagging remains the documented escalation path.
- **[New dependency]** `System.IO.Hashing` must trim/AOT-publish cleanly. → Mitigated by adding
  an `AotProbe` probe before relying on it in the main app, per existing project convention.
- **[Per-call allocation]** A new `XxHash3` instance per message is a small Gen0 allocation on
  the hot path. → Accepted: negligible next to the per-byte `HashCode.Add` loop removed, and
  avoids a correctness hazard from instance reuse.

## Migration Plan

Internal-only change, no persisted state or external interface involved. `_lastSeen`'s
`Dictionary<int, ...>` is in-memory and rebuilt from scratch on process start, so there is no
migration of existing keys — old and new hash values are simply never compared, since the
whole cache is process-lifetime only. Rollout is a normal code change; rollback is reverting
the commit.

## Open Questions

None outstanding — field-boundary delimiters were considered and deliberately deferred (see
Non-Goals).
