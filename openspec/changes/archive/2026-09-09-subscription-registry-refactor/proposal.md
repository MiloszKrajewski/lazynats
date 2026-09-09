## Why

`SubscriptionRegistry` stores each active subscription as a raw `(Pattern, Filter, Cts)` tuple and
separately re-materializes a public `SubscriptionInfo` record on every `Active` read, duplicating
the id/pattern shape. Its background `RunAsync` task also reaches directly into
`Services.Root.GetRequiredService<IApplication>()` to show an error `MessageBox`, and mutates the
`_subscriptions` dictionary from that background task on failure — both in tension with the class's
own "Add/Remove are UI-thread-only" invariant and its stated goal of not depending on Terminal.Gui.
Separately, its `catch (OperationCanceledException)` swallows *any* cancellation, not just the one
caused by its own `Cts` — an unrelated cancellation (e.g. connection teardown) leaks the entry in
`_subscriptions` forever, silently violating the existing `nats-subscriptions` spec requirement that
any non-deletion fault be reported and removed.

## What Changes

- Introduce `ISubscriptionInfo` (`Guid Id`, `string Pattern`); the existing `SubscriptionInfo`
  record keeps implementing it as the constructible DTO used by `SubscriptionsView` for pending
  add/edit values.
- Replace the registry's internal `(Pattern, Filter, Cts)` tuple with a concrete internal
  `Subscription` class implementing `ISubscriptionInfo` and additionally holding `Filter`/`Cts`;
  `Active` returns `IReadOnlyList<ISubscriptionInfo>` sourced directly (via interface covariance)
  from `_subscriptions.Values`, dropping the per-call `Select`/remap.
- `SubscriptionsView` (and `SubscriptionPatternPresenter`) switch their `ListEditorView<T>`/
  `IValuePresenter<T>` type argument from `SubscriptionInfo` to `ISubscriptionInfo`.
- Remove `SubscriptionRegistry`'s direct UI dependency: add a `Failed` event raised from `RunAsync`'s
  catch block instead of calling `Services.Root.GetRequiredService<IApplication>()` /
  `MessageBox.ErrorQuery` directly, and instead of mutating `_subscriptions` from the background
  task. `SubscriptionRegistry.cs` no longer references `Microsoft.Extensions.DependencyInjection` or
  `Terminal.Gui`.
- `SubscriptionsView` (which already owns the `Changed` subscription) subscribes to `Failed`,
  marshals to the UI thread, calls the existing `Remove(id)` (so dictionary cleanup and the
  `Changed` event both happen on the UI thread, honoring the class's own invariant), and shows the
  error dialog.
- Fix the cancellation-swallowing bug: `catch (OperationCanceledException) when
  (cancellationToken.IsCancellationRequested)` for the expected self-cancellation case; any other
  `OperationCanceledException` (or other exception) falls through to the `Failed`-raising path
  instead of leaking the entry.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-subscriptions`: the "Subscription Failure Reporting" requirement's normative text already
  covers a background-read fault "for any reason other than its own cancellation (deletion)", but
  had no scenario for a cancellation *not* caused by deletion (e.g. connection teardown) — today
  that case is silently swallowed and the entry leaks. Adds an explicit scenario documenting that
  this case is reported and removed like any other fault; no change to the requirement's normative
  text, just a fix so the implementation actually satisfies it.

## Impact

- `src/lazynats/Subscriptions/SubscriptionRegistry.cs`: storage type, `Active` shape, error
  reporting, cancellation handling.
- `src/lazynats/Subscriptions/SubscriptionInfo.cs` (or wherever the record lives): implements the
  new `ISubscriptionInfo`.
- `src/lazynats/Subscriptions/SubscriptionsView.cs`: generic type argument change, new `Failed`
  subscription + UI-thread marshaling + error dialog display.
- `src/lazynats/Subscriptions/SubscriptionPatternPresenter.cs`: generic type argument change.
- No changes to `SubscribeTab.cs`, `MainWindow.cs`, or `Program.cs` construction sites.
