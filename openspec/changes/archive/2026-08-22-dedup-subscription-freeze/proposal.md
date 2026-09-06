## Why

`MessageDeduplicator` collapses any two envelopes with the same `subject+headers+payload` hash
seen within a 50ms trailing window, regardless of which subscription produced them. That's
correct for its intended target — one physical publish delivered twice because two overlapping
subscription patterns both match it — but it also wrongly collapses genuinely distinct repeat
publishes of identical content, including ones landing on a single subscription, where core NATS
pub/sub never redelivers (so two identical envelopes on one `SubscriptionId` must be two separate
publishes). Observed directly: 10 rapid identical `nats pub` calls to one subject render as a
single row in the Live Feed. `doc/dedup.md` documents this as a known gap.

## What Changes

- `MessageDeduplicator` tracks the `SubscriptionId` of the surviving ("canonical") envelope
  alongside the timestamp for each dedup key. An incoming envelope is a duplicate only if its
  `SubscriptionId` differs from the currently stored canonical one (within the window).
- The stored canonical `(timestamp, SubscriptionId)` is updated **only when the incoming
  envelope passes through** (is not judged a duplicate). Duplicate hits leave it frozen.
  Freezing on duplicates — rather than refreshing on every call, as today — is what keeps the
  fix correct under 3+-way overlapping subscriptions and interleaved repeat publishes; refreshing
  on every hit was shown (see `design.md`) to both wrongly swallow and wrongly double-show
  repeats depending on arrival order.
- Accepted, documented limitation: if the canonical subscription for a key is removed and a
  genuine repeat publish arrives before that key's entry ages out of the window, it can still be
  wrongly suppressed (no delivering subscription can ever match the stale canonical id again).
  This self-heals once the entry ages past the window; see `design.md` Risks.
- `doc/dedup.md` is updated to describe the implemented behavior instead of a "not yet
  implemented" proposal.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `live-feed`: the "Duplicate Collapsing for Overlapping Subscriptions" requirement changes —
  subscription identity is no longer entirely excluded from dedup decisions. It stays out of the
  hash key itself, but is now tracked as comparison state: a same-subscription repeat within the
  window is no longer collapsed, while a cross-subscription echo still is.

## Impact

- `src/lazynats/LiveFeed/MessageDeduplicator.cs` — internal state shape (`_lastSeen` value type)
  and `IsDuplicate` logic change. No change to its public constructor signature, DI registration,
  or callers (`LiveUpdatesView`'s Rx `.Where(dedup.IsDuplicate)`).
  `FeedEnvelope`/`SubscriptionRegistry`/channel wiring are untouched.
- `doc/dedup.md` — updated to describe implemented behavior.
- Supersedes the "changing `MessageDeduplicator`'s dedup key or window semantics" non-goal
  recorded in the archived `rx-live-feed-pipeline` change; that non-goal was scoped to that
  change's own restructuring work, not a permanent constraint.
