## Context

`SubscriptionsView` and `PublishView`'s header editor each implement the same "text input + editable
list" shape independently, with real drift between them:

- `SubscriptionsView` keeps a `List<Guid> _ids` in parallel with the displayed `ObservableCollection<string>`
  to map list rows back to subscription identity — pure incidental bookkeeping.
- `PublishView`'s header editor uses two `TextField`s, a hand-rolled `HeaderListDataSource : IListDataSource`
  for banded-row rendering, and a nullable `_editingIndex` field to track in-place edits.
- Both bind their own Delete/Ctrl+N/E/D key handling ad hoc; `PublishView` already binds on the whole
  view (not per-child-control) specifically so the bindings fire regardless of which child has focus —
  a pattern worth generalizing rather than re-deriving per widget.

Separately, `MainWindow` already hand-builds per-widget status bar hints (`clearShortcut` wired to
`liveUpdates.HasFocusChanged`, `publishStatusShortcut` wired to `publishView.StatusChanged`) — proof
that focus-driven status bar content is a pattern the app wants, currently implemented by hand for each
widget with no shared mechanism.

Terminal.Gui v2.4.10 was inspected directly (via reflection against the installed assembly) to confirm
the primitives this design relies on actually exist as expected: `Application.Navigation.GetFocused()` /
`FocusedChanged`, `View.SuperView`, `View.KeyBindings.GetBindings()`, and `Shortcut.{Text,Key,Action,Visible}`.

## Goals / Non-Goals

**Goals:**
- A single reusable `ListEditorView<T>` that both current list-editing UIs could adopt later, eliminating
  the parallel-bookkeeping and hand-rolled-`IListDataSource` patterns.
- An app-wide, opt-in mechanism for a focused widget to advertise a handful of its own keyboard shortcuts,
  replacing the one-off `Shortcut` + `HasFocusChanged` wiring `MainWindow` currently repeats per widget.
- Both pieces buildable and structurally provable in isolation, without touching any existing view.

**Non-Goals:**
- Migrating `SubscriptionsView` or `PublishView` to `ListEditorView<T>` (follow-up change).
- Retrofitting `IShortcutSource` onto `SubscriptionsView`, `PublishView`, or `LiveUpdatesView` (follow-up
  change) — this also means `MainWindow`'s existing `clearShortcut`/`publishStatusShortcut` hand-wiring is
  left untouched for now, not replaced by the new aggregator.
- A header presenter parsing `"key: value"` is designed as the motivating example for the presenter
  contract, but is not wired into `PublishView` in this change.
- Multi-select in the list (single selection only, matching existing `ListView` usage elsewhere in the app).

## Decisions

### `ListEditorView<T>` is generic over `T`, with conversion injected as a presenter, not subclassed

Considered keeping the base class string-only and having each consumer maintain its own parallel typed
collection (as `SubscriptionsView` does today with `_ids`). Rejected — that's the exact duplication this
change exists to remove. Instead `ListEditorView<T>` owns `ObservableCollection<T>` directly, and a small
injected strategy object bridges `T` to display/input text:

```csharp
internal interface IValuePresenter<T>
{
    string Format(T value);
    bool TryParse(string raw, out T value, out string? error);
}
```

Injected via constructor rather than requiring a subclass per `T` — a future consumer with a simple `T`
(e.g. `string`) shouldn't need to declare a whole subclass just to supply an identity presenter.

### Core operations stay `protected virtual` on the editor itself, independent of the presenter

`Append`, `Edit`, `Delete`, `ClearInput` are virtual so a subclass can change *editor* behavior (e.g. what
"Enter" does) independent of how `T` is parsed/formatted. This is deliberately a second, orthogonal
extension point from the presenter — the presenter changes what a line *means*, virtual overrides change
what an *action* does.

### Validation is two-tier: continuous color hint, discrete error message

- Every keystroke in the input field re-runs `TryParse` and uses only the `bool` result to toggle the
  input's scheme between normal and invalid — reusing `PublishView`'s existing `InvalidSubject` red-text
  `Attribute` convention rather than introducing a second visual language for "this is wrong."
  Discards `value`/`error` from this call; it exists purely to answer "is current text valid right now."
- Only a real commit attempt (`Enter`) calls `TryParse` again and, on failure, invokes
  `protected virtual void OnParseError(string raw, string? error)`, default no-op. The live color already
  answers "is something wrong"; `OnParseError` is where a subclass can answer "what, specifically" (e.g.
  relay `error` into a `StatusChanged`-style event the way `PublishView.Send` already reports outcomes).

Considered validating only on `Enter` (no live color) — rejected as inconsistent with the existing subject
field convention in `PublishView`, which validates live. Considered a plain `bool TryParse` with no error
channel — rejected because `OnParseError` would have nothing to relay beyond "still invalid," making the
hook nearly useless for its intended purpose (surfacing *why*, e.g. showing "missing ':' separator" for
the header presenter).

### Key bindings live on the editor view itself, not on child controls

`Ctrl+D`/`Ctrl+E`/`Ctrl+N` are registered via `AddCommand`/`KeyBindings.Add` on `ListEditorView<T>`
directly, mirroring `PublishView`'s existing documented rationale: Terminal.Gui key-event dispatch bubbles
from the focused leaf up the `SuperView` chain, so binding at the container level means the shortcuts work
no matter whether the `TextField` or the `ListView` currently has focus, without needing per-child
duplication.

### Shortcut discoverability is opt-in via a dedicated interface, not by overloading `KeyBindings.Data`

`Terminal.Gui.Input.KeyBinding` has an unused `Data : object` slot that could carry a display label
per-binding, avoiding a new interface entirely. Rejected in favor of an explicit interface:

```csharp
internal interface IShortcutSource
{
    IEnumerable<ShortcutHint> Shortcuts { get; }
}

internal readonly record struct ShortcutHint(Key Key, string Text, Action Action);
```

Rationale: most `KeyBindings` entries on a real view — including framework-internal ones inherited by
built-in controls like `ListView`/`TextField` for arrow-key navigation — are not meant to be advertised.
Using `Data` as an implicit "show me" flag makes silence-by-default hard to audit (a binding is
discoverable unless someone remembers *not* to set `Data`); a dedicated interface makes the opt-in
explicit and reviewable at the type level (a view either declares `IShortcutSource` with a short curated
list, or it doesn't participate at all).

### Aggregation walks the focus chain from `Application.Navigation`, not a fixed per-widget wire-up

The aggregator subscribes once to `Application.Navigation.FocusedChanged`. On each change it walks
`GetFocused() → SuperView → ... → root`, collecting `Shortcuts` from every ancestor implementing
`IShortcutSource`, and rebuilds the set of shortcut hints available for that focus state. This replaces
the pattern of `MainWindow` constructing one `Shortcut` + one `HasFocusChanged` subscription per widget by
hand (as it does today for `clearShortcut` and `publishStatusShortcut`) with a single generic mechanism —
though per Non-Goals, wiring this aggregator into `MainWindow`'s live `StatusBar` and retiring the
existing hand-wired shortcuts is deferred to the follow-up migration change. In this change the aggregator
is built and structurally exercised (e.g. against `ListEditorView<T>` instances constructed directly), not
hooked into the running application's status bar yet.

## Risks / Trade-offs

- **[Risk]** Two orthogonal extension mechanisms (presenter for `T`↔string, virtual methods for editor
  behavior) could be confused by future maintainers about which one to override for a given need.
  → **Mitigation**: keep the split intentional and documented at the point of definition — presenter
  never controls *whether* something happens, only what a line *means*.
- **[Risk]** `IShortcutSource` requires every future component to remember to opt in explicitly; an author
  could add a genuinely useful new shortcut and simply forget to advertise it.
  → **Mitigation**: accepted trade-off — the alternative (advertise-everything-by-default) produces status
  bar noise from framework-internal bindings, which is worse for the stated keyboard-usability goal than
  an occasional missed opt-in that's easy to spot in review.
- **[Risk]** Building the aggregator without wiring it into `MainWindow` means it isn't validated against
  real focus-change churn in the running app during this change.
  → **Mitigation**: accepted, per explicit scope from the proposal; the follow-up migration change wires
  it in and is the point real end-to-end validation happens.

## Migration Plan

No migration — this change is additive only (new files), touches no existing view, and has no runtime
behavior change in the shipped app until the follow-up change wires either piece into
`SubscriptionsView`/`PublishView`/`MainWindow`. Rollback is simply reverting the new files.

## Open Questions

- Exact final shape of `ShortcutHint` (e.g. whether `Action` belongs on the hint itself vs. the aggregator
  re-resolving the underlying `Command` through the source view's own `KeyBindings`) is left to
  implementation; both are equivalent in behavior, this is a code-shape choice with no behavioral
  consequence for this change's scope.
- Where the header `"key: value"` presenter (and any consumer view built directly on `ListEditorView<T>`
  for this change's own verification) should live — as a small example/test-only artifact, or deferred
  entirely to the follow-up migration change — is left to `tasks.md` sequencing.
