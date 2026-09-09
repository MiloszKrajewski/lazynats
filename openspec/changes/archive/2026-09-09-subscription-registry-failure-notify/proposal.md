## Why

`SubscriptionRegistry.RunAsync`'s failure path (`SubscriptionRegistry.cs`) still removes the failed
entry from `_subscriptions` itself, from the background task, before raising `Failed` - despite the
class's own comment stating "Add/Remove (the public API) are only ever called from the UI thread."
`SubscriptionsView.OnRegistryFailed` compensates by manually splicing the failed item out of
`_items` directly instead of calling `_registry.Remove(id)`, so removal now happens through two
different code paths (`Changed`-driven `RefreshFromRegistry` for user-initiated deletes, and a
one-off manual `_items.Remove` for failures) instead of one. This is exactly the design the prior
`subscription-registry-refactor` change (`openspec/changes/archive/2026-09-09-subscription-registry-refactor/`)
called for but didn't land: the registry should only ever notify of failure; removal - dictionary
mutation, `Cts` cleanup, and `Changed` - should happen exclusively through `Remove(id)`, called by
the view in response to `Failed`.

## What Changes

- `SubscriptionRegistry.RunAsync`'s `catch (Exception ex)` block stops removing the entry from
  `_subscriptions` (no more `TryRemove`/`Cts.Dispose()` there); it looks up the still-present entry
  and raises `Failed?.Invoke(entry, ex)` only. If the entry is no longer present (e.g. a
  concurrent user-initiated delete already removed it), it raises nothing.
- `SubscriptionsView.OnRegistryFailed` stops splicing `_items` directly; it calls
  `_registry.Remove(subscription.Id)` (on the UI thread, via the existing `App!.Invoke`), which
  triggers `Remove`'s existing `Cts.Cancel()`/`Dispose()`/`Changed` path and lets the existing
  `RefreshFromRegistry` handler resync `_items`, then shows the error dialog.
- Since `_subscriptions` is now only ever mutated from the UI thread (`Add`/`Remove`, and `Remove`
  is now the only removal path, invoked either directly by the view or indirectly via
  `OnRegistryFailed`), re-evaluate whether `ConcurrentDictionary` is still needed or whether a plain
  `Dictionary` suffices given `RunAsync` only reads (via `TryGetValue`) rather than mutates the
  dictionary from the background thread; keep `ConcurrentDictionary` if a background-thread
  `TryGetValue` racing a UI-thread `Remove`/`Add` still needs it for safe concurrent access.
- Update the stale class comment above `SubscriptionRegistry` describing the old "RunAsync's
  failure path also removes its own entry directly" behavior to describe the corrected invariant.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-subscriptions`: the "Subscription Failure Reporting" requirement gains normative text and a
  scenario clarifying that a reported failure is removed through the same path as a user-initiated
  delete, so a subscription already removed (e.g. concurrently deleted by the user) by the time its
  fault is detected is not also reported as a failure. This was implicit and inconsistently
  implemented before; the change makes it explicit and consistently true.

## Impact

- `src/lazynats/Subscriptions/SubscriptionRegistry.cs`: `RunAsync`'s failure-catch block, the
  storage-type class comment, possibly the `_subscriptions` field type.
- `src/lazynats/Subscriptions/SubscriptionsView.cs`: `OnRegistryFailed` body and its stale comment.
- No changes to `SubscribeTab.cs`, `MainWindow.cs`, `Program.cs`, or any spec-level behavior.
