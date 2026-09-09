## 1. Registry stops removing on failure

- [x] 1.1 In `SubscriptionRegistry.RunAsync`'s `catch (Exception ex)` block, replace
      `if (!_subscriptions.TryRemove(id, out var entry)) return; entry.Cts.Dispose();
      Failed?.Invoke(entry, ex);` with `if (!_subscriptions.TryGetValue(id, out var entry)) return;
      Failed?.Invoke(entry, ex);` - the entry is no longer removed or disposed here.
- [x] 1.2 Update the class comment above `SubscriptionRegistry` (currently describing "RunAsync's
      failure path also removes its own entry directly, from a background-task thread...") to state
      the corrected invariant: `Remove` is the only method that mutates `_subscriptions`; `RunAsync`
      only reads it (`TryGetValue`) and raises `Failed`, relying on a subscriber to call `Remove`.
      Keep the explanation of why `ConcurrentDictionary` is still needed (background-thread reads
      racing UI-thread `Add`/`Remove`).

## 2. View removes through the registry

- [x] 2.1 In `SubscriptionsView.OnRegistryFailed`, replace the manual
      `_items.FirstOrDefault(...)`/`_items.Remove(...)` splice with a call to
      `_registry.Remove(subscription.Id)`, keeping the existing `App!.Invoke(...)` marshaling and
      the `MessageBox.ErrorQuery(...)` call (called after `Remove`, same message/title/button text
      as today).
- [x] 2.2 Update or remove the comment above `OnRegistryFailed` ("The registry has already removed
      `subscription` by the time this fires...") to reflect that `Remove` is now called from here
      and `_items` resyncs via the existing `Changed` → `RefreshFromRegistry` path, same as any
      other removal.

## 3. Verify

- [x] 3.1 `dotnet build src/lazynats.sln` — no compile errors/warnings from the changes.
- [x] 3.2 Manually (or via tmux) add and delete a subscription; confirm existing add/edit/delete
      behavior is unchanged.
- [x] 3.3 Manually trigger a subscription failure (e.g. an invalid/rejected pattern against a real
      server, or temporarily point at a down connection) and confirm the error dialog still appears
      and the subscription is removed from the list.
- [x] 3.4 `openspec validate subscription-registry-failure-notify --strict` passes.
