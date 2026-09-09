## Context

`SubscriptionRegistry.Add(pattern)` stores `pattern` verbatim and passes it unmodified to
`NatsConnection.SubscribeAsync<byte[]>(pattern, ...)` inside a fire-and-forget background task
(`RunAsync`). `PatternDialog`, used by `SubscriptionsView` for both New and Edit, is constructed
today with no `validator`, so any non-empty string is accepted. `RunAsync`'s `catch` clause only
matches `OperationCanceledException` — any other failure (invalid subject shape, auth/permission
rejection, a dropped connection) is unhandled inside that background task.

The app already has a relaxed pattern grammar for this exact shape of problem:
`lazynats.Core.FilterExpression.TryCompile(string)` (`openspec/specs/kv-filter-expression/spec.md`),
used today by every list's `F` search (`ListEditorView`, `DrillableListView`, `ValuesTab`). It
compiles a pattern into a `NatsFilter(Native, Client, NativeFilterIsExact)`: `Native` is always a
syntactically valid NATS subject (safe to hand to the NATS client), `Client` is a `Regex` for exact
matching against candidates, and `NativeFilterIsExact` says whether `Native` alone already resolves
every match (so `Client` can be skipped).

## Goals / Non-Goals

**Goals:**
- Subscriptions accept the same relaxed pattern grammar `F` search already uses, with identical
  semantics (users learn one grammar, not two).
- Reuse `FilterExpression` verbatim — no new grammar/parsing code.
- A subscription that can't actually be started (bad runtime conditions, not bad grammar — grammar
  is already caught at input time) reports its failure via a dialog instead of failing silently,
  and no longer appears in the active list.

**Non-Goals:**
- No change to the KV filter grammar itself (`kv-filter-expression` spec is unchanged).
- No change to the "delete subscription doesn't confirm" behavior (separate TODO item, out of
  scope here).
- No retry/reconnect logic for a subscription that fails after starting — a failure still just
  stops that subscription, same as cancellation does today.

## Decisions

### Reuse `FilterExpression`, don't invent a subscription-specific grammar
`FilterExpression` already does exactly what's needed (native-subject derivation +
over-approximation + exact-match regex) and is already spec'd and battle-tested via `F` search.
A second, subscription-specific grammar would mean two mental models for "relaxed pattern" in the
same app for no behavioral benefit.

### Compile once in `SubscriptionRegistry.Add`, store the compiled filter
`Add` calls `FilterExpression.TryCompile(pattern)` and stores the resulting `NatsFilter` alongside
the original `pattern` string in the `_subscriptions` dictionary (replacing the current
`(string Pattern, CancellationTokenSource Cts)` tuple with a `(string Pattern, NatsFilter Filter,
CancellationTokenSource Cts)` one). `RunAsync` subscribes to `Filter.Native` and, only when
`!Filter.NativeFilterIsExact`, checks `Filter.Client.IsMatch(subject)` per received message before
forwarding it — skipping the regex check entirely on the (common) exact-native-match path, same
optimization `ListEditorView`/`DrillableListView` already apply.

`SubscriptionInfo.Pattern` (what the list displays and what Edit pre-fills) keeps showing the
original relaxed pattern the user typed, never the derived native subject — no visible change to
today's list/edit UI.

Alternative considered: recompile the pattern on every message. Rejected — pointless per-message
allocation for a value that never changes for the lifetime of a subscription.

### Validate input with `FilterExpression.TryCompile(p) is not null`
`SubscriptionsView.TryEditPattern` passes `validator: p => FilterExpression.TryCompile(p) is not
null` into `PatternDialog`, the same predicate `ListEditorView`/`DrillableListView` already use for
their `F` filter fields. This is the only invalid input `FilterExpression` recognizes (empty segments —
leading/trailing/doubled `.`); everything else compiles, so this mostly guards against obviously
malformed input while typing, with the dialog's existing red-text feedback.

### Failures surface via `MessageBox.ErrorQuery`, resolved lazily via `Services.Root`
`SubscriptionRegistry` is constructed with `new` before `Services.Configure` runs (see
`Program.cs`), so it can't take `IApplication` as a constructor dependency. Instead, `RunAsync`'s
catch block (broadened from `OperationCanceledException` to also catch `Exception`) resolves
`Services.Root.GetRequiredService<IApplication>()` lazily, matching the existing
`CreateKeyDialog`-style convention for non-View, non-DI-constructed classes reaching UI services.
On any non-cancellation failure: remove the subscription's entry from `_subscriptions`, raise
`Changed` (so the list stops showing it as active), and `app.Invoke(() => MessageBox.ErrorQuery(app,
" Subscription Failed ", ex.Message.Pad(), "_Ok"))` — mirroring the exact pattern already used in
`StreamsTab`/`ObjectsTab`/`TemplatesTab`.

This resolution only ever happens from a real received-or-attempted subscription, which in
practice only occurs after `MainWindow` is running (i.e. well after `Services.Configure`) — the
`#if DEBUG` startup `registry.Add(">")` subscribes with an already-native-valid, always-startable
pattern, so it can't hit this path before `Services.Configure` runs.

## Risks / Trade-offs

- **A relaxed pattern that isn't natively exact broadens the actual NATS-level subscription** (e.g.
  a single non-terminal wildcarded token collapses everything from that point to `>`), so the
  client may receive (and immediately discard) more server traffic than a hand-written native
  subject would. → Same trade-off already accepted for `F` search; acceptable since it only
  applies to patterns the user deliberately wrote in relaxed form, not existing exact ones (which
  hit the `NativeFilterIsExact` fast path unchanged).
- **A subscription that fails after being added disappears from the list without the user having
  dismissed anything first** if they're not looking at the screen when it faults — the error
  dialog still appears (modal, so it'll be seen next time the app has focus), but the list update
  isn't undoable. → Matches existing behavior for other async list operations in the app
  (`StreamsTab` etc. also just refresh/remove on failure after showing the dialog).

## Migration Plan

No data migration; in-memory-only state. Existing active subscriptions (added via already-native
patterns) behave identically after this change — they hit the `NativeFilterIsExact` fast path with
no client-side filtering, same as today.
