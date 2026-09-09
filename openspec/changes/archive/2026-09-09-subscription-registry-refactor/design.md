## Context

`SubscriptionRegistry` (`src/lazynats/Subscriptions/SubscriptionRegistry.cs`) owns one background
task per active subscription pattern. Today:

- Its dictionary value is a raw `(string Pattern, NatsFilter Filter, CancellationTokenSource Cts)`
  tuple; `Active` separately builds a `SubscriptionInfo` record per entry on every read.
- `RunAsync`'s failure path reaches into `Services.Root.GetRequiredService<IApplication>()` to show
  a `MessageBox.ErrorQuery` directly, and removes the failed entry from `_subscriptions` — both from
  the background task, contradicting the file's own "Add/Remove are UI-thread-only" comment and
  giving a non-View, non-DI-constructed class a Terminal.Gui/DI dependency.
- `catch (OperationCanceledException) { }` swallows any cancellation unconditionally, not just the
  one caused by its own `Cts.Cancel()` from `Remove()` — an unrelated cancellation leaks the entry.

`SubscriptionsView` (`src/lazynats/Subscriptions/SubscriptionsView.cs`) already owns the
`registry.Changed` subscription and is a `View` with `App` available, making it the natural place
to own UI-facing reactions to the registry.

## Goals / Non-Goals

**Goals:**
- Registry storage is a concrete type, not a tuple; the type exposed to consumers via `Active` is
  narrower than the storage type (hides `Filter`/`Cts`).
- `SubscriptionRegistry` has no Terminal.Gui or `Microsoft.Extensions.DependencyInjection`
  dependency; failure reporting is via a plain event.
- Only genuinely-self-caused cancellation is swallowed; any other cancellation is treated as a
  fault and reported/removed, matching the existing "Subscription Failure Reporting" requirement.
- No change to `SubscribeTab`, `MainWindow`, or `Program.cs` construction sites, and no observable
  behavior change beyond the cancellation-leak fix.

**Non-Goals:**
- Not changing how patterns are compiled/filtered (`FilterExpression`/`NatsFilter`) or how messages
  reach the feed.
- Not introducing a generic pub/sub or mediator abstraction — a single typed event is enough for
  the one current listener.

## Decisions

**1. `ISubscriptionInfo` interface, `Subscription` concrete storage type, `SubscriptionInfo` DTO
kept.**
`ISubscriptionInfo { Guid Id { get; } string Pattern { get; } }` is the shape exposed by `Active`.
The registry's dictionary becomes `Dictionary<Guid, Subscription>` where `internal sealed class
Subscription : ISubscriptionInfo` also carries `Filter` and `Cts` (registry-internal, not exposed).
`Active` becomes:
```csharp
public IReadOnlyList<ISubscriptionInfo> Active => _subscriptions.Values.ToList();
```
`List<Subscription>` is assignable to `IReadOnlyList<ISubscriptionInfo>` via generic interface
covariance (`Subscription : ISubscriptionInfo`), so this drops the old `Select(kv => new
SubscriptionInfo(...))` remap entirely — one list allocation instead of a list + N record
allocations.

The existing `SubscriptionInfo` record (`Guid Id, string Pattern`) stays, now implementing
`ISubscriptionInfo`, because `SubscriptionsView.TryEditPattern` needs a constructible value to
represent a pending add/edit (`new SubscriptionInfo(Guid.Empty, pattern)`) before the registry
assigns a real id — a plain interface can't be `new`'d for that placeholder.

*Alternative considered*: make `Subscription` itself the only type and drop `SubscriptionInfo`,
having `SubscriptionsView` pass pattern strings around instead of placeholder values. Rejected -
`ListEditorView<T>`'s `TryCreate`/`TryEdit`/`Add`/`Replace` shape is built around producing/
consuming a `T` value, and reworking that generic contract is out of scope for this change.

**2. `SubscriptionsView`/`SubscriptionPatternPresenter` retarget to `ISubscriptionInfo`.**
`SubscriptionsView : ListEditorView<SubscriptionInfo>` becomes `ListEditorView<ISubscriptionInfo>`;
`SubscriptionPatternPresenter : IValuePresenter<SubscriptionInfo>` becomes
`IValuePresenter<ISubscriptionInfo>`. `ListEditorView<T>` has no `new()`/equality constraint on
`T` (verified in `Components/ListEditorView.cs`), so this is a mechanical signature change.

**3. `Failed` event replaces the direct `MessageBox`/DI call; `SubscriptionsView` owns the UI
reaction.**
```csharp
public event Action<Guid, Exception>? Failed;
```
raised from `RunAsync`'s catch block instead of calling `Services.Root...`/`MessageBox` and
instead of touching `_subscriptions` directly. `SubscriptionsView` — already subscribed to
`Changed` — adds:
```csharp
_registry.Failed += OnRegistryFailed;
...
private void OnRegistryFailed(Guid id, Exception ex) =>
    App!.Invoke(() => {
        _registry.Remove(id);
        MessageBox.ErrorQuery(App!, " Subscription Failed ", ex.Message.Pad(), "_Ok");
    });
```
Routing cleanup through the real `Remove(id)` (called from the UI thread via `App!.Invoke`, matching
how `TryEditPattern` already uses `App!.Run(dialog)`) means the dictionary mutation and `Changed`
event both now genuinely happen on the UI thread, fixing the pre-existing violation of the class's
own invariant as a side effect.

*Alternative considered*: put the `Failed` listener on `SubscribeTab` instead of
`SubscriptionsView`. Rejected — `SubscriptionsView` already owns the `Changed` wiring and is where
`MessageBox`/`App` usage already exists (`TryEditPattern`), so colocating `Failed` there keeps all
registry-reactive UI code in one place rather than splitting it across the tab and the view.

**4. Cancellation-filter fix.**
```csharp
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // Expected when Remove() cancels this subscription's token - already cleaned up.
}
catch (Exception ex)
{
    Failed?.Invoke(id, ex);
}
```
`cancellationToken` here is the token passed into `RunAsync`, i.e. `entry.Cts.Token` - so the
`when` filter is true only when *this subscription's own* `Cts` was the one cancelled. Any other
`OperationCanceledException` (or any other exception) falls through to the general `catch`, which
now always goes through `Failed` (no separate dictionary mutation inline).

## Risks / Trade-offs

- [`Failed` is raised from a background-task thread, same as the old direct `MessageBox` call was]
  → `SubscriptionsView.OnRegistryFailed` marshals via `App!.Invoke` before touching the registry or
  showing UI, same pattern already used elsewhere in the codebase.
- [Widening the general `catch (Exception ex)` to also catch foreign `OperationCanceledException`
  changes behavior for that specific edge case] → intentional per proposal; covered by the new
  scenario in `specs/nats-subscriptions/spec.md`.

## Open Questions
(none)
