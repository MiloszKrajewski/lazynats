## Why

Wildcard subscriptions (`>`, `*.>`, etc.) currently pull in NATS's own system traffic
(`$SYS.>`, `$JS.>`, `$KV...`) and reply-inbox chatter (`_INBOX.>`) alongside the user's own
subjects, drowning the live feed in noise the user almost never wants to see. Since these
subjects are identifiable by a fixed prefix, a subscription that didn't ask for them explicitly
shouldn't receive them.

## What Changes

- Each active subscription implicitly excludes messages whose subject starts with `$` unless the
  subscription's own pattern also starts with `$`.
- Each active subscription implicitly excludes messages whose subject starts with `_INBOX.`
  unless the subscription's own pattern also starts with `_INBOX.`.
- This exclusion is per-subscription (based on that subscription's own pattern text), applied
  before a message is wrapped into a `FeedEnvelope`, so excluded messages never enter the shared
  feed pipeline and are invisible to dedup/rendering.
- A subscription pattern that explicitly opts into one of these namespaces (e.g. `$SYS.>` or
  `_INBOX.>`) continues to receive matching traffic as normal — the exclusion only applies when
  the pattern itself doesn't ask for that namespace.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-subscriptions`: Add Subscription now implicitly filters out `$`-prefixed and
  `_INBOX.`-prefixed subjects per-subscription, unless the subscription's own pattern opts into
  that namespace.

## Impact

- `src/lazynats/Subscriptions/SubscriptionRegistry.cs` (`RunAsync`): add the implicit
  namespace-exclusion check alongside the existing client-side pattern filter, before an envelope
  is constructed and pushed to the sink.
- No change to `FilterExpression`/`NatsFilter` — the exclusion is based on the raw pattern
  string's own prefix, not the compiled native/client filter.
