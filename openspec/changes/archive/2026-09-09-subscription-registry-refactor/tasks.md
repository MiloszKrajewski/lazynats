## 1. Interface and concrete storage type

- [x] 1.1 Add `src/lazynats/Subscriptions/ISubscriptionInfo.cs` with `internal interface
      ISubscriptionInfo { Guid Id { get; } string Pattern { get; } }`.
- [x] 1.2 In `SubscriptionRegistry.cs`, make the existing `SubscriptionInfo` record implement
      `ISubscriptionInfo`.
- [x] 1.3 In `SubscriptionRegistry.cs`, add `internal sealed class Subscription : ISubscriptionInfo`
      with `Id`, `Pattern`, `Filter` (`NatsFilter`), `Cts` (`CancellationTokenSource`).
- [x] 1.4 Change `_subscriptions` from `Dictionary<Guid, (string Pattern, NatsFilter Filter,
      CancellationTokenSource Cts)>` to `Dictionary<Guid, Subscription>`; update `Add`/`Remove`/
      `RunAsync` to construct/read the new type instead of the tuple.
- [x] 1.5 Change `Active` to `public IReadOnlyList<ISubscriptionInfo> Active =>
      _subscriptions.Values.ToList();`, dropping the old `Select(kv => new SubscriptionInfo(...))`.

## 2. Retarget consumers to `ISubscriptionInfo`

- [x] 2.1 Change `SubscriptionPatternPresenter : IValuePresenter<SubscriptionInfo>` to
      `IValuePresenter<ISubscriptionInfo>` in `SubscriptionPatternPresenter.cs`.
- [x] 2.2 Change `SubscriptionsView : ListEditorView<SubscriptionInfo>` to
      `ListEditorView<ISubscriptionInfo>` in `SubscriptionsView.cs`, including the
      `ObservableCollection<SubscriptionInfo>` field/constructor parameter and `TryCreate`/
      `TryEdit`/`Add`/`Replace`/`Delete`/`RefreshFromRegistry` signatures (constructing
      `new SubscriptionInfo(...)` still works since it implements the interface).
- [x] 2.3 Build and confirm no other call site depends on `SubscriptionInfo` specifically instead
      of `ISubscriptionInfo` (check `SubscribeTab.cs`, `MainWindow.cs`).

## 3. Decouple `SubscriptionRegistry` from the UI

- [x] 3.1 Add `public event Action<Guid, Exception>? Failed;` to `SubscriptionRegistry`.
- [x] 3.2 In `RunAsync`'s `catch (Exception ex)` block, remove the `_subscriptions.Remove(...)` call,
      the `Changed?.Invoke()` call, and the `Services.Root.GetRequiredService<IApplication>()`/
      `MessageBox.ErrorQuery` call; replace with `Failed?.Invoke(id, ex);`.
- [x] 3.3 Remove the now-unused `using Microsoft.Extensions.DependencyInjection;`,
      `using Terminal.Gui.App;`, `using Terminal.Gui.Views;` from `SubscriptionRegistry.cs`
      (keep `using lazynats.Core;`/`using lazynats.LiveFeed;`/`using NATS.Client.Core;` as needed).
- [x] 3.4 In `SubscriptionsView.cs`, subscribe to `_registry.Failed += OnRegistryFailed;` alongside
      the existing `_registry.Changed += RefreshFromRegistry;`, and unsubscribe both in `Dispose`.
- [x] 3.5 Implement `OnRegistryFailed(Guid id, Exception ex)` on `SubscriptionsView`: marshal via
      `App!.Invoke(...)`, call `_registry.Remove(id)`, then `MessageBox.ErrorQuery(App!,
      " Subscription Failed ", ex.Message.Pad(), "_Ok")` (same message/title/button text as today).

## 4. Fix the cancellation-swallowing leak

- [x] 4.1 Change `RunAsync`'s `catch (OperationCanceledException) { }` to `catch
      (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }`, keeping
      the existing comment (adjusted to note this is the self-cancellation case).
- [x] 4.2 Confirm a foreign `OperationCanceledException` (one where `cancellationToken` was not the
      one requesting cancellation) now falls through to the general `catch (Exception ex)` block
      and raises `Failed`, instead of being silently swallowed.

## 5. Verify

- [x] 5.1 `dotnet build src/lazynats.sln` — no compile errors/warnings from the type changes.
- [x] 5.2 Manually (or via tmux) add/edit/delete a subscription and confirm existing behavior is
      unchanged.
- [x] 5.3 Manually trigger a subscription start-up failure (e.g. an invalid/rejected pattern against
      a real server, or temporarily point at a down connection) and confirm the error dialog still
      appears and the subscription is removed from the list.
- [x] 5.4 `openspec validate subscription-registry-refactor --strict` passes.
