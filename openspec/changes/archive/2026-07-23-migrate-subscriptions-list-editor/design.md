## Context

`SubscriptionsView` (`src/lazynats/SubscriptionsView.cs`) currently maintains `ObservableCollection<string>
_patterns` for display alongside a parallel `List<Guid> _ids` purely to map a selected row back to the
`SubscriptionRegistry` identity needed for `Remove`. `SubscriptionRegistry` (`src/lazynats/
SubscriptionRegistry.cs`) is the real source of truth: `Add(string pattern)` assigns a new `Guid`, starts
a real NATS subscription, and fires `Changed` synchronously before returning; `Remove(Guid id)` cancels
the subscription and fires `Changed` synchronously; `Active : IReadOnlyList<SubscriptionInfo>` (where
`SubscriptionInfo` is `record(Guid Id, string Pattern)`) is the current snapshot. `SubscriptionsView`
today rebuilds both `_patterns` and `_ids` from `Active` on every `Changed` event.

`ListEditorView<T>` (`src/lazynats/ListEditorView.cs`, from the prior `add-list-editor-shortcuts` change,
now archived) assumes a simpler ownership model: it owns `ObservableCollection<T>` directly and mutates
it in place from `Append`/`Edit`/`Delete`. `SubscriptionsView` doesn't fit that model as-is — the registry,
not the view, must own the actual add/remove side effects (starting/stopping a NATS subscription), and
the view's collection is a mirror that gets resynced via the `Changed` event, exactly as today.

## Goals / Non-Goals

**Goals:**
- Eliminate the parallel `List<Guid> _ids` bookkeeping by using `SubscriptionInfo` (which already carries
  `Id`) as `ListEditorView<T>`'s type parameter, instead of `string`.
- Preserve `SubscriptionRegistry` as the sole source of truth: all add/remove still goes through it, and
  the view's collection remains a resynced mirror, matching today's `Changed`-driven refresh pattern.
- Add Ctrl+E as a discoverable convenience for changing a pattern, implemented as remove-old-then-add-new
  against the registry — not a new domain capability, just a shorter path to what
  `nats-subscriptions` already allows.

**Non-Goals:**
- Any change to `SubscriptionRegistry` itself, or to how NATS subscriptions are started/stopped.
- True in-place subscription editing (not possible — a NATS subscription is bound to a subject pattern at
  creation).
- Wiring `IShortcutSource`/the shortcut aggregator into `MainWindow`'s live `StatusBar` (still deferred,
  as noted in the prior change).
- Migrating `PublishView`'s header editor to `ListEditorView<T>` (separate future work).

## Decisions

### Type parameter is `SubscriptionInfo`, not `string`

Using `T = SubscriptionInfo` means each item the `ListEditorView<T>` list holds already carries the `Id`
needed for `Remove`, via `_items[index].Id` — no separate identity-to-index mapping required. A small
presenter bridges it to text:

```csharp
internal sealed class SubscriptionPatternPresenter: IValuePresenter<SubscriptionInfo>
{
    public string Format(SubscriptionInfo value) => value.Pattern;

    public bool TryParse(string raw, out SubscriptionInfo value, out string? error)
    {
        var pattern = raw.Trim();
        if (pattern.Length == 0) { value = default!; error = "pattern must not be empty"; return false; }
        value = new SubscriptionInfo(Guid.Empty, pattern);
        error = null;
        return true;
    }
}
```

`Guid.Empty` is a placeholder that is never persisted — `SubscriptionsView`'s overridden `Append` never
lets the base class's default "just store the parsed value" path run; it always routes through the
registry, which assigns the real id.

### `SubscriptionsView` keeps its own reference to the same `ObservableCollection<SubscriptionInfo>` it hands to the base constructor

The base class's `_items` field is private (by design — `ListEditorView<T>` doesn't assume every consumer
needs external write access to it). `SubscriptionsView` needs to resync that exact collection from
`SubscriptionRegistry.Active` on every `Changed` event, so it keeps its own field pointing at the same
instance, threaded through a private constructor overload:

```csharp
internal sealed class SubscriptionsView: ListEditorView<SubscriptionInfo>
{
    private static readonly SubscriptionPatternPresenter Presenter = new();

    private readonly SubscriptionRegistry _registry;
    private readonly ObservableCollection<SubscriptionInfo> _items;

    public SubscriptionsView(SubscriptionRegistry registry)
        : this(registry, new ObservableCollection<SubscriptionInfo>(registry.Active)) { }

    private SubscriptionsView(SubscriptionRegistry registry, ObservableCollection<SubscriptionInfo> items)
        : base(items, Presenter)
    {
        _registry = registry;
        _items = items;
        _registry.Changed += RefreshFromRegistry;
    }
    // ...
}
```

`Presenter` is `static readonly` (stateless) so it doesn't need the same constructor-chaining treatment —
only the per-instance collection does. This was chosen over adding a `protected ObservableCollection<T>
Items` accessor to `ListEditorView<T>` because `SubscriptionsView` is the only consumer that needs it so
far, and the base class shouldn't grow write-access surface for a need only one subclass has; if a second
consumer needs the same thing, revisit.

### `ListEditorView<T>` gains a `protected int? EditingIndex` read accessor

The only piece of base-class state a subclass genuinely needs and can't reconstruct itself is "is a commit
right now an edit of an existing item, or a fresh add" — that's exactly what the private `_editingIndex`
field already tracks. Exposing it read-only:

```csharp
protected int? EditingIndex => _editingIndex;
```

is additive, changes no existing behavior, and avoids `SubscriptionsView` re-deriving or duplicating that
bookkeeping itself (which would reintroduce the exact kind of parallel state this migration is trying to
remove).

### `Append` and `Delete` are fully overridden, not composed with the base implementation

The base class's default `Append`/`Delete` directly mutate `_items` — wrong for `SubscriptionsView`, where
`_items` must only ever reflect `SubscriptionRegistry.Active`. Both are overridden to talk to the registry
instead, relying on `Changed` firing synchronously (confirmed in `SubscriptionRegistry.Add`/`Remove`,
which call `Changed?.Invoke()` before returning) so `RefreshFromRegistry` has already resynced `_items` by
the time the override method returns:

```csharp
protected override void Append(string raw)
{
    if (!Presenter.TryParse(raw, out var value, out var error)) {
        OnParseError(raw, error);
        return;
    }

    if (EditingIndex is { } index && index < _items.Count) _registry.Remove(_items[index].Id);
    _registry.Add(value.Pattern);
    ClearInput();
}

protected override void Delete(int index)
{
    if (index >= _items.Count) return;
    _registry.Remove(_items[index].Id);
    if (index == EditingIndex) ClearInput();
}
```

`Edit(int index)` is NOT overridden — the inherited default (format the item via the presenter into the
input, track the index, focus the input) is already correct, since `Format` just returns the pattern text.

An edit-commit therefore fires `Changed` twice in a row (once from `Remove`, once from `Add`), each
triggering a full `RefreshFromRegistry`. This is accepted as harmless — the same double-refresh would
happen today if a user manually deleted then re-added a pattern as two separate actions; bundling them
into one keystroke doesn't change the cost, just the number of user gestures.

### The "_Add" button is removed, not preserved alongside the list editor

It was already fully redundant with `Enter` in the pattern field (`_patternField.Accepted` already called
the same `AddFromField`). `ListEditorView<T>` has no button by design (matches `PublishView`'s header
editor and `doc/UI.md`'s "no mouse-only affordances" rule for this class of control), so keeping one just
for `SubscriptionsView` would be inconsistent without adding any real capability.

## Risks / Trade-offs

- **[Risk]** Ctrl+E's "edit" is a UI-level convenience over delete-then-add; a user might expect the
  subscription's identity to be preserved (e.g. if anything downstream keyed off subscription `Guid`
  across an edit). → **Mitigation**: nothing today keys off subscription `Guid` outside `SubscriptionRegistry`
  itself and `FeedEnvelope` tagging of already-received messages (which is inherently tied to whichever
  subscription received them, and is correct either way); the modified spec requirement makes this
  explicit rather than leaving it implicit.
- **[Risk]** `EditingIndex` becoming `protected` slightly widens `ListEditorView<T>`'s inheritance surface
  before there's a second consumer needing it, which could turn out to be the wrong shape once `PublishView`
  is eventually migrated too. → **Mitigation**: it's a read-only accessor over already-existing private
  state, the smallest possible change; revisit if the next consumer needs something different.

## Migration Plan

No runtime migration — `SubscriptionsView` is constructed fresh each app run from `SubscriptionRegistry`,
which is unaffected. Rollback is reverting `SubscriptionsView.cs` and the one-line `ListEditorView.cs`
addition.

## Open Questions

- None outstanding; all decisions above were confirmed during exploration before this change was
  proposed.
