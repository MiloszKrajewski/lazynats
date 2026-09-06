## 1. Implementation

- [x] 1.1 Change `MessageDeduplicator._lastSeen`'s value type from `DateTimeOffset` to a
      `(DateTimeOffset Timestamp, Guid SubscriptionId)` tuple (or equivalent small struct).
- [x] 1.2 In `IsDuplicate`, compute `isDuplicate` using both the window check and
      `SubscriptionId` comparison against the stored canonical value, **before** touching
      `_lastSeen` — mirror the existing lookup-then-decide ordering already in the method.
- [x] 1.3 Write the new `(now, envelope.SubscriptionId)` into `_lastSeen[key]` only inside the
      `!isDuplicate` branch; leave the stored entry untouched when `isDuplicate` is true.
- [x] 1.4 Update `Prune` (or its call site) if needed for the new value type — it only needs the
      `Timestamp` component, so confirm it still compiles/reads correctly against the tuple.
- [x] 1.5 Update the file-level comment in `MessageDeduplicator.cs` that currently cites "design.md's
      Dedup decision" (from the archived `add-subscriptions-feed` change) — point it at this
      change's `design.md` instead, since the decision it's describing has changed.

## 2. Documentation

- [x] 2.1 Update `doc/dedup.md`: replace the "Known gap" and "Proposed refinement (not yet
      implemented)" sections with a description of the implemented frozen-canonical-`SubscriptionId`
      behavior, including the accepted residual risk (canonical subscription removed + repeat
      arrives within the window) and its self-healing bound.

## 3. Verification

- [x] 3.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 3.2 Manual repro via the `.bin/` `nats` CLI + a running `lazynats` (tmux-driven per
      `CLAUDE.md`): with a single subscription active, fire several rapid identical `nats pub`
      calls to one subject — confirm each now appears as its own row (previously collapsed to one).
- [x] 3.3 Manual repro: with two overlapping subscription patterns active (e.g. `foo.>` and
      `foo.bar`) and a single publish to `foo.bar`, confirm the feed still shows exactly one row
      (existing cross-subscription collapsing behavior preserved).
- [x] 3.4 Manual repro: with the same two overlapping patterns active, fire two rapid identical
      publishes to `foo.bar` — confirm the feed shows exactly two rows (one per physical publish),
      not one and not three.
