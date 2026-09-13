## Context

`SubscriptionRegistry.RunAsync` (`src/lazynats/Subscriptions/SubscriptionRegistry.cs`) already
has a per-subscription gate: for relaxed patterns whose native NATS filter over-approximates, it
runs `clientFilter.IsMatch(message.Subject)` before wrapping a message into a `FeedEnvelope` and
pushing it to the shared feed. A wildcard pattern like `>` or `*.>` has no such over-approximation
to narrow — its native filter is already exact — so today it receives everything the server sends
it, including `$SYS.>`/`$JS.>`/`$KV...` system traffic and `_INBOX.>` reply subjects.

## Goals / Non-Goals

**Goals:**
- Suppress `$`-prefixed and `_INBOX.`-prefixed subjects from a subscription that didn't
  explicitly ask for that namespace, before they become `FeedEnvelope`s.
- Preserve the ability to see that traffic by subscribing to a pattern that itself starts with
  `$` or `_INBOX.`.

**Non-Goals:**
- No configurability of the excluded prefixes (no settings/env var) — the two prefixes are fixed,
  matching NATS's own reserved namespaces.
- No change to `FilterExpression`/`NatsFilter` or the native-subject compilation — the exclusion
  is a separate, independent gate, not part of the relaxed-pattern grammar.
- No retroactive filtering of anything already in the feed buffer/dedup state — this only affects
  what gets pushed into the pipeline going forward.

## Decisions

**Gate on the raw pattern string's own prefix, not the compiled `NatsFilter`.** The opt-in check
(“did this subscription ask for the namespace?”) is simplest and cheapest as a direct
`Pattern.StartsWith(...)` on the string the user typed, evaluated once per subscription (not per
message). This mirrors the trivial two-line shape from the proposal and avoids re-deriving intent
from the compiled native/client filter, which exists to answer a different question (server-side
scoping vs. exact match), not "did the user ask for `$...`".

**Implement as a second, always-on predicate alongside `clientFilter`, not folded into it.** The
existing `clientFilter` is `null` for native-exact patterns (skipping regex work when the native
subject already matches exactly). The namespace exclusion must run for *every* subscription,
native-exact or not, so it's a separate `bool` computed once before the receive loop starts
(`excludeSystem = !pattern.StartsWith('$')`, `excludeInbox = !pattern.StartsWith("_INBOX.")`),
checked inline per message alongside the existing `isMatch` check, short-circuiting before any
regex work.

**Check is prefix-only, not per-segment.** `$` only matters as the very first character of the
subject (NATS reserves subjects whose *first token* starts with `$`, e.g. `$SYS`, `$JS`, `$KV`,
`$O`); `_INBOX.` is checked as a literal string prefix, matching NATS's default inbox subject
shape. No segment-aware wildcard matching is needed for either check.

## Risks / Trade-offs

- [Server uses a non-default inbox prefix (`--inbox_prefix` or per-connection `InboxPrefix`)] →
  Not handled; `_INBOX.` is the NATS default and this app doesn't currently read or expose a
  configured inbox prefix. Accepted as a known gap matching the proposal's trivial-implementation
  scope; revisit if the app ever supports custom inbox prefixes.
- [A pattern that legitimately starts with a literal `$` or `_INBOX.`-like token but isn't meant
  as an opt-in] → Not a real concern: `$` and `_INBOX.` are already reserved/special in NATS
  subject space, so no ordinary application subject collides with them.
