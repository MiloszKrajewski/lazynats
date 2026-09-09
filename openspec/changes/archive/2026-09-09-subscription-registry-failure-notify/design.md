## Context

`SubscriptionRegistry` (`src/lazynats/Subscriptions/SubscriptionRegistry.cs`) owns one background
task per active subscription pattern (`RunAsync`). Today, on a background-read fault:

```csharp
catch (Exception ex)
{
    if (!_subscriptions.TryRemove(id, out var entry)) return;

    entry.Cts.Dispose();
    Failed?.Invoke(entry, ex);
}
```

The registry removes the entry itself, from the background task, then notifies. `SubscriptionsView`
(`src/lazynats/Subscriptions/SubscriptionsView.cs`) reacts by splicing `_items` directly:

```csharp
private void OnRegistryFailed(ISubscriptionInfo subscription, Exception ex) =>
    App!.Invoke(() => {
        var item = _items.FirstOrDefault(x => x.Id == subscription.Id);
        if (item is not null) _items.Remove(item);
        MessageBox.ErrorQuery(App!, " Subscription Failed ", ex.Message.Pad(), "_Ok");
    });
```

This gives removal two independent code paths: `Remove(id)` (dictionary mutation + `Cts`
cleanup + `Changed` → `RefreshFromRegistry`) for user-initiated deletes, and this one-off `_items`
splice for failures, which never touches the dictionary's `Cts` disposal via `Remove` and never
raises `Changed`. It also means `_subscriptions` is mutated from both the UI thread (`Add`/`Remove`)
and the background task (`RunAsync`'s catch), which is what motivated `ConcurrentDictionary` over a
plain `Dictionary` in the first place - see the class comment. This is the same problem the earlier
`subscription-registry-refactor` change (`openspec/changes/archive/2026-09-09-subscription-registry-refactor/`)
already designed a fix for; its `design.md` calls for exactly the split below, but the shipped code
diverged from it during implementation.

## Goals / Non-Goals

**Goals:**
- `SubscriptionRegistry` never mutates `_subscriptions` outside of `Add`/`Remove`; `RunAsync`'s
  failure path only raises `Failed`.
- All removal - dictionary mutation, `Cts.Cancel()`/`Dispose()`, `Changed` - happens exclusively
  through `Remove(id)`, regardless of whether the trigger is a user-initiated delete or a reported
  failure.
- `SubscriptionsView.OnRegistryFailed` becomes a thin wrapper: marshal to the UI thread, call
  `Remove`, show the dialog - `_items` resyncs via the existing `Changed`/`RefreshFromRegistry` path
  like any other removal, with no separate splice logic.
- No observable behavior change: a failed subscription is still reported via the same dialog and
  still disappears from the list.

**Non-Goals:**
- Not changing `Add`, pattern compilation/filtering, or the cancellation-vs-fault distinction
  (`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)`) already in
  place from the prior refactor.
- Not introducing a generic mediator/event-bus abstraction for the single `Failed` listener.

## Decisions

**1. `RunAsync`'s catch block looks up, does not remove.**
```csharp
catch (Exception ex)
{
    if (!_subscriptions.TryGetValue(id, out var entry)) return;
    Failed?.Invoke(entry, ex);
}
```
`Cts.Dispose()` moves out of this block entirely - it now only happens inside `Remove`, which is
the sole place that owns the entry's lifecycle end-to-end. If the entry is no longer present (e.g.
a user deleted it in the same window the background task was already failing), nothing is raised -
matches today's `Remove`-wins-races behavior, just via `TryGetValue` instead of `TryRemove`.

*Alternative considered*: keep removing in `RunAsync` but also raise `Changed`, so `_items` stays in
sync without the view needing to call `Remove`. Rejected - this still leaves two removal code
paths (the dictionary mutation still happens in two different methods), which is the exact
duplication this change removes; it also still mutates `_subscriptions` from a background thread.

**2. `SubscriptionsView.OnRegistryFailed` calls `Remove`, drops the manual `_items` splice.**
```csharp
private void OnRegistryFailed(ISubscriptionInfo subscription, Exception ex) =>
    App!.Invoke(() => {
        _registry.Remove(subscription.Id);
        MessageBox.ErrorQuery(App!, " Subscription Failed ", ex.Message.Pad(), "_Ok");
    });
```
`Remove` is idempotent-safe to call here even though the entry might already be gone (e.g. a
concurrent user delete) since it already no-ops via its own `TryRemove` check. Calling `Remove`
before showing the dialog means `_items` (via the synchronously-raised `Changed` →
`RefreshFromRegistry`) is already updated by the time the modal `MessageBox.ErrorQuery` call blocks,
matching the existing ordering (item disappears, then the dialog is shown).

*Alternative considered*: raise `Changed` from within `RunAsync`'s catch block directly instead of
routing through the view calling `Remove`. Rejected - `Changed` and `Failed` would then need to fire
together in a specific order from a background thread, recreating exactly the cross-thread
dictionary-mutation concern this change removes; routing through the view's existing `App!.Invoke`
→ `Remove` call keeps every `_subscriptions` mutation and `Changed` raise on the UI thread, as the
class's own invariant already requires for `Add`/`Remove`.

**3. Keep `ConcurrentDictionary`.**
Even after this change, `RunAsync` (background thread) calls `TryGetValue` on `_subscriptions`
while `Add`/`Remove` (UI thread, including the `Remove` called from `OnRegistryFailed`) mutate it
concurrently. A plain `Dictionary` is not safe for a reader on one thread racing a writer on
another, so `ConcurrentDictionary` stays; only the *removal* responsibility moves to the UI thread,
not all access to the dictionary.

**4. Update the class comment.**
The current comment ("RunAsync's failure path also removes its own entry directly, from a
background-task thread, so the registry can guarantee cleanup on its own rather than relying on a
Failed subscriber to call Remove()") is replaced with one stating the corrected invariant: `Remove`
is the only method that mutates `_subscriptions`; `RunAsync` only reads it and raises `Failed`,
relying on a subscriber to call `Remove`. `ConcurrentDictionary` remains necessary because that read
still races the UI-thread `Remove`/`Add` calls.

## Risks / Trade-offs

- [If no `Failed` subscriber calls `Remove`, a failed subscription would now linger in
  `_subscriptions` forever instead of self-cleaning] → acceptable: `SubscriptionRegistry` currently
  has exactly one consumer (`SubscriptionsView`), which is required by this change to call `Remove`
  in its `Failed` handler; this mirrors the already-accepted trade-off from the prior refactor's
  design (decoupling the registry from directly owning UI-independent cleanup).
- [Between the fault occurring and the view's `App!.Invoke` callback running, `Active` still
  reports the failed-but-not-yet-removed subscription] → same window exists today implicitly for
  `Add`'s own registration; not user-observable since nothing polls `Active` on a timer outside of
  the view's own event-driven refresh.

## Open Questions
(none)
