## Context

`ListEditorView<T>` (`src/lazynats/Components/ListEditorView.cs`) currently owns a `TextField`
above its `ListView` and drives everything through it: `Append` parses the field's text via an
injected `IValuePresenter<T>.TryParse` on Enter, `Edit` loads the selected item's `Format`ted text
back into the field, `ClearInput` resets it (also cancelling an in-progress edit), and the field is
live-validated on every keystroke. Two controls sharing one component means focus has to be
explicitly handed between them — hence the `Up`-at-top-of-list-focuses-the-input requirement added
in `tab-navigation-and-shortcuts`, and the arrow-key regression that motivated this change: any
future change to arrow-key routing (in `Tabs`, in `ListView`, in an ancestor) is a chance to reopen
that same coordination bug.

`SubscriptionsView` is the only current subclass exercising this (`PublishView`'s header editor is
a separate, parallel hand-rolled implementation, out of scope here). The Streams/Consumers/KV/OBJ
tabs referenced in `doc/UI.md` don't exist yet, but are expected to reuse `ListEditorView<T>`, and
their items are likely to need more than one input field (e.g. a stream's name *and* subjects *and*
retention policy) — a single shared `TextField` doesn't generalize to that regardless of the
navigation issue.

## Goals / Non-Goals

**Goals:**
- Remove the inline `TextField` and all state that exists solely to coordinate it with the list
  (`_editingIndex`, `ClearInput`, live-validation attribute, the Up-focuses-input key binding).
- Give New (Ctrl+N) and Edit (Ctrl+E) a callback shape that a subclass implements by running a
  modal `Dialog`, so item construction/editing can use as many fields/widgets as `T` needs.
- Keep `Delete` (Ctrl+D) and the keyboard-shortcut-discovery (`IShortcutSource`) contract unchanged
  in shape — only what New/Edit *do* changes, not that they exist or how they're discovered.
- Keep the callback idiom consistent with `IValuePresenter<T>.TryParse`'s existing
  bool-plus-`out`-result shape, so the codebase has one convention for "attempt X, report success,
  hand back a value" rather than two.

**Non-Goals:**
- Rewriting `PublishView`'s header editor onto `ListEditorView<T>` (it isn't one today; unifying it
  is a separate, later change if wanted).
- Building any of the Streams/Consumers/KV/OBJ tabs themselves — this only prepares the base class
  they'll subclass.
- Async/background modal flows. Terminal.Gui `Dialog`s are already run synchronously and blocking
  elsewhere in this codebase (`MessageBox.Query` in `MainWindow.cs`), so `TryCreate`/`TryEdit` stay
  synchronous too.

## Decisions

### Two abstract methods, not one nullable-original method
Considered a single `bool TryEdit(T? original, out T result)` where `New` passes a null/default
"no original" sentinel. Rejected: `T` is unconstrained and spans both reference types
(`SubscriptionInfo`, a `sealed record`) and value types (`HeaderPair` in `PublishView`, a
`readonly record struct`) across current and future presenters/items. Expressing "no value" for an
unconstrained `T` needs either a `where T : struct`/`where T : class` split or a wrapper type,
either of which is more machinery than just having two methods:

```csharp
protected abstract bool TryCreate(out T result);
protected abstract bool TryEdit(T original, out T result);
```

### Obtaining a value is separate from committing it: `Add`/`Replace` alongside `TryCreate`/`TryEdit`
Discovered while implementing `SubscriptionsView`: it doesn't own `_items` as a source of truth —
`_items` is a mirror of `SubscriptionRegistry`, refreshed via the registry's `Changed` event. If
`TryCreate`/`TryEdit` succeeding always caused the base class to also append/replace directly into
`_items` (as the first version of this design and its spec draft assumed), a successful create would
double up: once via `_registry.Add` synchronously firing `Changed` → `RefreshFromRegistry` →
`_items.Add`, and again via the base class's own post-callback append. Split the concern instead:
`TryCreate`/`TryEdit` (abstract) obtain a value from the user; separate `protected virtual void
Add(T value)` / `Replace(int index, T value)` (default: mutate `_items` directly) commit it.
`SubscriptionsView` overrides `Add`/`Replace` (and keeps overriding `Delete`, unchanged) to redirect
into the registry instead, exactly mirroring the pre-change code's rationale for making `Append`/
`Edit`/`Delete` overridable in the first place — only now the "how do I get a value" and "how do I
commit it" steps are two separate override points instead of one.

### Abstract methods on `ListEditorView<T>`, not injected delegates
Considered constructor-injected `Func`-like delegates (matching how `IValuePresenter<T>` is
injected today). Rejected in favor of `protected abstract` methods because every other
per-action customization point on this type (`Append`, `Edit`, `Delete`, `ClearInput` today) is
already an overridable method, not an injected callback, and every current/anticipated consumer
(`SubscriptionsView` today; Streams/Consumers/KV/OBJ tomorrow) is a dedicated subclass rather than
a generic reusable instance — so there's no case where injecting instead of overriding buys
anything, and mixing both customization mechanisms on one type would be inconsistent for no
benefit. `abstract` (not `virtual` with a default) because the base class has no reasonable default
modal to fall back to for an arbitrary `T`.

### `Try*` naming, not `On*`
`On*` conventionally names a void event hook; this is a query that reports success and hands back a
value, which is exactly what `TryParse` (already on `IValuePresenter<T>`) does. Naming these
`TryCreate`/`TryEdit` makes the convention explicit and reusable rather than introducing a second,
differently-named pattern for the same shape.

### `IValuePresenter<T>` keeps `Format`, drops `TryParse`
`PresenterListDataSource<T>` still needs `Format` to render list rows regardless of how items are
edited. `TryParse` has no caller left once there's no free-text input to parse — parsing/validating
whatever the modal collects is that modal's own concern (e.g. the new pattern-entry `Dialog` in
`SubscriptionsView` validates its own text field before letting the user commit).

### `SubscriptionsView`'s Ctrl+E modal
Replaces the current "load pattern into shared input, Enter removes-old-and-adds-new" flow with a
`PatternDialog: Dialog<string>` (Terminal.Gui 2.4.10's generic `Dialog<TResult>`, whose `Result`
property already is exactly the "committed value or null-for-cancelled" shape this change wants —
used directly instead of hand-rolling separate `Committed`/`Value` properties) containing one
`TextField` seeded from `original.Pattern` (empty for create), a Cancel/OK `Button` pair (Cancel
added before OK, so OK is last-added-becomes-default per this codebase's documented dialog-button
convention), and the same existing pattern validation gating the OK button before it can commit.
`SubscriptionsView.Add`/`Replace` (see the `Add`/`Replace` decision above) still perform the actual
remove-old-add-new-identity behavior in `nats-subscriptions`' "Modifying a Subscription Pattern"
requirement, unchanged — only how the new pattern text is collected changes.

**Construction-order pitfall found while implementing:** wiring `_patternField.ValueChanged` before
setting its initial `Text` (e.g. to set `Text` as a statement after `Add(...)` for clarity) fires
the change handler *during construction*, before `_okButton` exists yet, causing a
`NullReferenceException` inside `UpdateValidity()`. Fixed by constructing `_okButton` first (so it
exists no matter what fires first) and setting the `TextField`'s initial `Text` via its object
initializer, before `ValueChanged` is subscribed at all — the safer general pattern for any view
that both seeds initial state and reacts to that state changing.

## Risks / Trade-offs

- **Modal interrupts flow more than an inline field for rapid-fire edits.** Adding several
  subscriptions in a row now means Ctrl+N → dialog → OK → Ctrl+N → dialog → OK instead of
  type-Enter-type-Enter. Mitigated by: this is also true today for *editing* (Ctrl+E already
  requires deliberate action) and the win (no shared-focus coordination bugs, room for multi-field
  items) outweighs the extra keystroke for the add-a-few-patterns case.
- **Every future list-editor subclass must build its own dialog** rather than getting text-entry
  for free. Mitigated by: a trivial single-field case (like the new `SubscriptionsView` dialog) is
  a handful of lines; nothing here prevents factoring a shared "single-text-field dialog" helper
  later if the pattern repeats across Streams/Consumers/KV/OBJ, but that's speculative until those
  tabs exist.
- **`PublishView`'s parallel hand-rolled header editor now diverges further** from
  `ListEditorView<T>` conventions (it still uses the old inline-input shape). Not addressed here;
  flagged as a candidate for a later unification change.

## Migration Plan

Single-PR refactor, no data/schema migration:
1. Add `TryCreate`/`TryEdit` abstracts and strip the inline-input machinery from
   `ListEditorView<T>`.
2. Drop `TryParse` from `IValuePresenter<T>`.
3. Update `SubscriptionsView`/`SubscriptionPatternPresenter` to the new shape, including the new
   pattern-entry `Dialog`.
4. Manually re-verify Ctrl+N/Ctrl+E/Ctrl+D and arrow-key navigation in the Subscribe tab (no
   automated UI test project exists yet).
No rollback concerns beyond reverting the commit — no persisted state changes shape.

## Open Questions

- Should a shared "single-text-field entry dialog" helper be factored out now (used by
  `SubscriptionsView`'s new modal) even though it currently has only one caller, anticipating that
  Streams/KV `name`-only-style items will want the same thing? Leaning no until a second caller
  exists, but flagging since it's cheap to do now vs. later.
