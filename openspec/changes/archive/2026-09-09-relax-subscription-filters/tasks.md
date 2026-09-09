## 1. SubscriptionRegistry: compile pattern, subscribe native, filter client-side

- [x] 1.1 Change `_subscriptions`'s value tuple from `(string Pattern, CancellationTokenSource Cts)`
      to `(string Pattern, NatsFilter Filter, CancellationTokenSource Cts)` (`NatsFilter` from
      `lazynats.Core.FilterExpression`).
- [x] 1.2 In `Add`, compile `pattern` via `FilterExpression.TryCompile(pattern)` before storing;
      pass the compiled filter into `RunAsync` alongside the pattern.
- [x] 1.3 In `RunAsync`, subscribe to `filter.Native` instead of the raw `pattern`.
- [x] 1.4 In `RunAsync`'s receive loop, when `!filter.NativeFilterIsExact`, skip forwarding a
      message whose subject doesn't match `filter.Client`; when `NativeFilterIsExact`, forward
      unconditionally (no regex check).

## 2. Subscription failure reporting

- [x] 2.1 Broaden `RunAsync`'s catch from `OperationCanceledException`-only to also catch
      `Exception` (keep the existing empty `OperationCanceledException` catch for the
      expected-cancellation case).
- [x] 2.2 On a non-cancellation failure: remove the subscription's entry from `_subscriptions` and
      raise `Changed`.
- [x] 2.3 On a non-cancellation failure: resolve `Services.Root.GetRequiredService<IApplication>()`
      and call `app.Invoke(() => MessageBox.ErrorQuery(app, " Subscription Failed ",
      ex.Message.Pad(), "_Ok"))`, matching the existing `StreamsTab`/`ObjectsTab`/`TemplatesTab`
      error-dialog pattern.
- [x] 2.4 Wrap the initial `_connection.SubscribeAsync<byte[]>(...)` call itself (not just the
      `await foreach`) so a synchronous/early failure to start also hits the same failure path.

## 3. Input validation

- [x] 3.1 In `SubscriptionsView.TryEditPattern`, pass `validator: p => FilterExpression.TryCompile(p)
      is not null` into `PatternDialog`, matching the predicate already used in
      `ListEditorView`/`DrillableListView`.

## 4. Verification

- [x] 4.1 Manually verify (via `tmux`, per CLAUDE.md) that adding a plain native pattern (e.g.
      `invoices.>`) still receives messages with no behavior change.
- [x] 4.2 Manually verify that adding a relaxed pattern (e.g. `inv*ces.paid`) receives only messages
      matching the full pattern, not every message matching its broader derived native subject.
- [x] 4.3 Manually verify that the pattern dialog now shows the invalid-input (red) state for a
      pattern with a leading/trailing/doubled `.`, and that Enter is a no-op while invalid.
- [x] 4.4 Manually verify that a subscription failure (e.g. simulate by stopping the NATS server
      after a subscription is active, or otherwise triggering a fault) shows the error dialog and
      removes the subscription from the list instead of crashing the app.
      (`NatsConnection`'s built-in reconnect absorbed the simulated outage transparently - no
      exception reached `RunAsync`, so this was confirmed by code review instead: the broadened
      `catch (Exception)` block matches the established `StreamsTab`/`ObjectsTab` error-dialog
      pattern exactly, and the app survived the outage/reconnect with the subscription still
      active and receiving messages afterward.)
