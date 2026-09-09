## Why

The Subscribe pattern field accepts any non-empty text with no grammar check, and
`SubscriptionRegistry` hands that text straight to `NatsConnection.SubscribeAsync` as a literal
NATS subject. A pattern that isn't valid native NATS subject syntax (e.g. a wildcard mixed
mid-token, like an `F`-search-style `he*lo` search) either does nothing useful or throws inside the
subscription's fire-and-forget background task, which today only catches
`OperationCanceledException` — any other failure (bad subject shape, a permission error, a dropped
connection) is unhandled and gives the user no feedback; the subscription just silently stops
receiving messages while still showing as "active" in the list.

The app already has a relaxed filter-expression grammar (`FilterExpression`, `*`/`?`/`>` wildcards
usable inline within a token, not just as a bare token) used for every list's `F` search. Wiring
subscriptions through that same grammar — deriving a real NATS subject to subscribe with plus an
optional client-side regex to narrow matches — gives subscriptions the same relaxed matching for
free, and is a small, well-contained change.

## What Changes

- The Subscribe pattern field (new/edit) now validates input against the existing
  `FilterExpression` grammar instead of accepting any non-empty text.
- `SubscriptionRegistry` compiles a subscription's pattern via `FilterExpression.TryCompile`,
  subscribes to the compiled native NATS subject, and — when the pattern isn't natively exact —
  filters each received message's subject against the compiled regex before forwarding it into the
  feed.
- A subscription's displayed/edited pattern is still the original relaxed pattern the user typed,
  unchanged from today.
- Any failure while starting or running a subscription (subscribe call throws, or the background
  read loop faults for a reason other than cancellation) is reported to the user via a
  `MessageBox.ErrorQuery` dialog (matching the pattern already used in `StreamsTab`) instead of
  going unhandled, and the failed subscription is removed from the active list.

## Capabilities

### New Capabilities

(none — the filter grammar itself is already specified in `kv-filter-expression` and is reused
as-is)

### Modified Capabilities

- `nats-subscriptions`: "Add Subscription" now accepts the relaxed `FilterExpression` grammar
  (compiling to a native subject plus optional client-side filter) rather than requiring literal
  native NATS subject syntax; the pattern edit modal validates against this grammar; a new
  requirement covers reporting subscription failures to the user instead of failing silently.

## Impact

- `src/lazynats/Subscriptions/SubscriptionRegistry.cs`: compile pattern via `FilterExpression`,
  subscribe to the native subject, apply client-side filtering, surface failures.
- `src/lazynats/Subscriptions/SubscriptionsView.cs`: pass a `FilterExpression`-based validator into
  `PatternDialog`.
- `src/lazynats/Core/FilterExpression.cs`: reused as-is, no changes expected.
- `openspec/specs/nats-subscriptions/spec.md`: updated via delta spec.
