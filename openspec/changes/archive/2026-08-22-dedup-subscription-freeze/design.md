## Context

`MessageDeduplicator` (`src/lazynats/LiveFeed/MessageDeduplicator.cs`) sits in the live feed's Rx
chain (`Where(dedup.IsDuplicate)`) and today keys purely on `hash(subject + headers + payload)`:
a message is a duplicate if the same key was seen within a trailing 50ms window, full stop.
`SubscriptionId` and `ReceivedAt` are both excluded from the key entirely — the original design
(`2026-07-22-add-subscriptions-feed`) reasoned that the only realistic duplicate source is one
physical publish matching two *overlapping* subscription patterns, where `SubscriptionId` is
*expected* to differ between the two envelopes, so including it would defeat the match.

That reasoning missed a second real case: genuinely repeated publishes of identical content.
Core NATS pub/sub has no server-side redelivery, so if the *same* `SubscriptionId` sees identical
content twice within the window, those are necessarily two distinct publishes, not a delivery
artifact — yet today they're silently collapsed to one row (`doc/dedup.md`'s observed repro: 10
rapid identical `nats pub` calls → 1 feed row).

`MessageDeduplicator.IsDuplicate` is only ever called from one logical place at a time (downstream
of `Subject.Synchronize()` in the live feed pipeline), so no locking is introduced or required by
this change — that invariant, established in `rx-live-feed-pipeline`, is preserved as-is.

## Goals / Non-Goals

**Goals:**
- Stop collapsing genuine same-subscription repeats, while still collapsing genuine
  cross-subscription echoes of one physical publish — the dedup's original purpose.
- Keep correct under realistic overlap: 2+ subscription patterns matching the same subject, in
  any arrival order, including repeated identical content while the overlap is active.
- No new locking, no new dependency, no change to `MessageDeduplicator`'s constructor signature
  or its callers.

**Non-Goals:**
- Changing the dedup key itself (`subject + headers + payload` hash) or the window value (50ms).
  Only the duplicate-decision logic and the state it tracks change.
- Fully eliminating every theoretical swallow scenario. A narrow, self-healing residual risk
  (see Risks) is accepted rather than solved with heavier per-key state.
- Surfacing "matched by: X, Y" provenance in the UI — noted as a future option in the original
  design, still not built here.

## Decisions

### Track one frozen "canonical" `SubscriptionId` per key, not a set of contributing subscriptions

`_lastSeen`'s value becomes `(Timestamp, SubscriptionId)` instead of just `Timestamp`. An
incoming envelope is a duplicate iff an entry exists, is within the window, **and** its
`SubscriptionId` differs from the stored one.

**Alternative considered and rejected: track the full set of subscription ids that echoed the
current key within the window.** This was the first fix explored. It's more obviously "correct"
on paper (each subscription only ever counted once per physical message) but is unnecessary
complexity — the frozen-single-id approach below is provably sufficient for a *stable* overlap
set, which is the realistic case, and is a much smaller change (one field, not a nested
dictionary).

### Freeze the stored `(Timestamp, SubscriptionId)` on duplicate hits — only update it on pass-through

This is the load-bearing decision. The naive version of "compare against the last stored
`SubscriptionId`" — updating the stored value on *every* call, duplicate or not, mirroring
today's unconditional `_lastSeen[key] = now` — is broken under realistic ≥2-way overlap combined
with repeated identical content. Two concrete counterexamples, both with a stable, unchanged
overlapping-subscription set (no subscription add/remove involved):

**Swallow**, 3-way overlap (A, B, C), two genuine physical publishes M1 then M2, arriving
`A1,B1,C1,A2,B2,C2`:
```
 event   compare-to-last(naive)   result   last-after
 A1      (empty)                   PASS     A
 B1      B≠A                       DUP      B
 C1      C≠B                       DUP      C
 A2      A≠C  ← wrong               DUP      A   -- M2's own canonical wrongly suppressed
 B2      B≠A                       DUP      B
 C2      C≠B                       DUP      C
```
M2 produces zero rows — not merely misattributed, entirely absent — because `A2` compares against
`C`, the leftover tail state from M1's chain, not against "has A already delivered this batch."

**Leak**, 2-way overlap (A, B), same two publishes, arriving `A1,A2,B1,B2` (A's task happens to be
faster than B's for both):
```
 event   compare-to-last(naive)   result   last-after
 A1      (empty)                   PASS     A
 A2      A==A                      PASS     A   -- correct, genuine repeat on same subscription
 B1      B≠A                       DUP      B
 B2      B==B  ← wrong              PASS     B   -- extra duplicate row for M2
```
3 rows for 2 physical messages, because `B2`'s "same as last" happened to match `B1`'s leftover
state, not `A2`.

Freezing the stored value — updating it **only when the current call is not a duplicate** —
eliminates both failures. Re-run with freezing:
```
 A1 PASS→(t0,A)   B1 DUP,unchanged   C1 DUP,unchanged
 A2: A==A(frozen) → PASS →(t20,A)    B2 DUP,unchanged   C2 DUP,unchanged
```
M1→A1, M2→A2: exactly 2 rows. And for the leak case:
```
 A1 PASS→(t0,A)   A2: A==A → PASS→(t1,A)   B1: B≠A → DUP,unchanged   B2: B≠A → DUP,unchanged
```
Exactly 2 rows, no leak.

**Why this generalizes**: for a *stable* overlapping-subscription set, whichever subscription
"wins" as canonical for one physical message is necessarily also a subscriber of the next
physical message (same overlap set), so that same subscription's own delivery of the next message
will match the still-frozen canonical id and pass through — regardless of what order the other
subscribers' echoes happen to arrive in, since duplicates no longer perturb the frozen state.
Timestamp is frozen alongside the id (not refreshed on duplicate hits) so a stale entry still ages
out strictly `window` after the canonical event, not indefinitely extended by a stream of
duplicate echoes.

**Alternative considered and rejected: refresh the timestamp on duplicate hits but freeze only
the `SubscriptionId`.** Rejected because it lets a continuous stream of cross-subscription echoes
keep a stale/defunct canonical id alive indefinitely, widening the residual risk below from a
bounded `window`-length exposure into an unbounded one.

## Risks / Trade-offs

- **[Risk] Removed-subscription swallow window.** If the canonical subscription for a key is
  unsubscribed, and a genuine repeat publish (same content) arrives before that key's frozen
  entry ages out of the 50ms window, every copy of the repeat is compared against the now-defunct
  canonical id, always differs, and is wrongly suppressed — the repeat produces zero rows.
  → **Mitigation**: accepted as-is. The exposure is strictly bounded to ≤ window (50ms) after the
  subscription removal and self-heals the moment the stale entry ages out (the next arrival then
  hits the "no valid entry" branch and passes through fresh). Revisit only if real usage shows
  this occurring often enough to matter — e.g. by clearing a key's entry outright when its
  canonical subscription is removed, rather than waiting for natural expiry.
- **[Trade-off] Extreme fan-out delivery skew.** The "generalizes" argument above assumes a single
  physical publish's fan-out across its matching subscriptions arrives within roughly the same
  instant, well inside the 50ms window. If scheduling delay ever stretched one subscription's copy
  of a single physical message to arrive more than `window` after another, the frozen entry could
  expire mid-fan-out and produce an extra row for what is still one physical message.
  → **Mitigation**: none needed now — no evidence this happens in practice at a 50ms window; the
  existing design already carried an equivalent assumption (all echoes of one publish arrive close
  together) and this change doesn't make it materially worse.

## Migration Plan

Single-PR, in-process pipeline change only, no persisted state or rollback concerns. Sequence:
change `_lastSeen`'s value type and `IsDuplicate`'s update logic in `MessageDeduplicator.cs` →
update its file-level comment (currently cites "design.md's Dedup decision" from the *archived*
`add-subscriptions-feed` design; point it at this change instead) → update `doc/dedup.md` to
describe implemented behavior → update `openspec/specs/live-feed/spec.md` via this change's
delta.

## Open Questions

None outstanding — the state-shape alternative (full subscription set vs. frozen single id) and
the residual-risk trade-off were resolved during exploration before this change was proposed.
