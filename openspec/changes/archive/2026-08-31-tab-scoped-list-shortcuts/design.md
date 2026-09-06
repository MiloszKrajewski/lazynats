## Context

`DrillableListView<T>` and `ListEditorView<T>` each bind Ctrl+R/Ctrl+N/Ctrl+D/Ctrl+E (and, ad hoc,
`KeyListView`/`ObjectListView` bind Ctrl+F) as `KeyBindings` on themselves. Terminal.Gui v2's key
dispatch (`doc/terminal-gui-howto.md`; confirmed against the framework docs) recurses into the
focused subview first, then — only once that recursion returns unhandled — checks the current
view's own `KeyBindings`, then bubbles to its `SuperView`, and so on up to the root. So a binding
placed directly on `DrillableListView<T>` only ever fires when the currently-focused view is that
list or one of its own descendants.

That's a real gap in every drill-down tab (`StreamsTab`, `ValuesTab`, `ObjectsTab`): each keeps
**two** lists alive at once (e.g. `_listView`/`_consumerListView` in `StreamsTab.cs:20,26`) plus a
details pane per level (`_details`/`_consumerDetails`), all added as siblings under the tab
(`StreamsTab.cs:90`). Tab/Shift+Tab can land focus on the details pane, which shares no
ancestor/descendant relationship with the active list — only the tab itself is an ancestor of
both. `SubscribeTab` (`ListEditorView<T>`-backed, single list, no sibling pane) doesn't have this
gap today, but is included for architectural uniformity — see Decision 5.

`DrillableListView<T>.Shortcuts` (`DrillableListView.cs:337-349`) and
`ListEditorView<T>.Shortcuts` (`ListEditorView.cs:188-192`) already compute exactly "which of
these operations does this list instance currently support, with what label and invocable
action" — gated by the same private enabled-flags that gate the `KeyBindings.Add` calls. That's
the one existing piece of plumbing this design leans on hardest.

## Goals / Non-Goals

**Goals:**
- Ctrl+N/Ctrl+D/Ctrl+R/Ctrl+E/Ctrl+F work anywhere focus is inside a management tab, not only
  while the currently-relevant list itself holds focus.
- A tab only ever advertises (`IShortcutSource.Shortcuts`, feeding Alt+K) the operations its
  *currently active* list actually supports right now — not a static union of everything the tab
  could ever show across both its levels.
- One mechanism, reused by dispatch and by exposure, so they can't drift apart (the thing checked
  before invoking an operation is the same thing checked before advertising it).
- Uniform shape across all four tabs, so a future tab gets this for free by following the same
  pattern rather than reinventing it.

**Non-Goals:**
- The `/` quick in-memory fuzzy search and Esc/Backspace ascend stay exactly as they are today —
  list-focused, opt-in via `AttachFilterBox`/`EnableAscend`. They only make sense while the list
  itself (or its attached `FilterBox`) has focus, unlike "do this to the whole visible list"
  operations.
- `PublishDialog`/`HeaderEditorView` (a modal, not a tab) is untouched — there is no owning tab to
  hoist its bindings to.
- No change to `ShortcutAggregator`/`ShortcutPickerDialog` internals — they keep walking the
  focused-ancestor chain and collecting `IShortcutSource.Shortcuts` exactly as today; only which
  views supply which hints changes.

## Decisions

### 1. The tab becomes the sole owner of dispatch, via `OnKeyDownNotHandled` — not a hardcoded key list

`DrillableListView<T>.EnableCreate/EnableDelete/EnableEdit` and the base constructor's Ctrl+R
wiring keep everything they do *except* the `KeyBindings.Add` call on `this` — they still flip the
`_createEnabled`/`_deleteEnabled`/`_editEnabled` flags (still needed for `Shortcuts`), and
`EnableCreate` still strips the inner native `ListView`'s conflicting Ctrl+N→`Command.Down` alias
(`DrillableListView.cs:122`) — that conflict is between the list and *its own* child, unrelated to
where the outer action is bound, and left in place it would swallow Ctrl+N before it ever bubbles
past the inner `ListView`. `KeyListView`/`ObjectListView` drop their own
`KeyBindings.Add(Key.F.WithCtrl, Command.Open)`.

`ListEditorView<T>` is different: it's shared by `SubscriptionsView` (tab-hosted, moving) *and*
`HeaderEditorView` inside the standalone `PublishDialog` modal (not moving — Non-Goal). The base
constructor gains a `bool bindSharedKeys = true` parameter gating its three `KeyBindings.Add`
calls (`ListEditorView.cs:54-56`); the `AddCommand` registrations stay unconditional either way, so
both the `Shortcuts` and (new, see Decision 2) `TabOperations` computations always have something
to read from. `HeaderEditorView` doesn't pass the parameter (defaults `true`, unchanged behavior);
`SubscriptionsView` passes `bindSharedKeys: false`. This keeps the "opt out of self-binding, stay
correct by default" shape consistent with how `DrillableListView<T>`'s `Enable*()` methods already
work — nothing here needs a subclass to remember to do the *removal*, only `SubscriptionsView`
needs to remember the opt-out, which is a one-line, one-call-site change.

The owning tab does **not** hardcode which keys it forwards (an earlier draft of this design had
each tab `KeyBindings.Add`-ing a fixed Ctrl+R/N/D/E/F set and dispatching through a `Dispatch(Key)`
helper — rejected during implementation review for baking in a fixed key list the tab has no
business assuming). Instead each tab overrides `View.OnKeyDownNotHandled(Key key)` — the same
"nothing more specific claimed this key" fallback hook Terminal.Gui's own built-in views
(`TextField`, `TextView`, `TableView`, `HexView`, `MenuBarItem`, `PopoverMenu`, ...) already use for
exactly this role, confirmed via the framework docs (`docs/keyboard.html`: `NewKeyDownEvent`
recurses into the focused SubView first, then — only once that returns unhandled — runs
`OnKeyDown`/`KeyDown`/`KeyBindings`/`HotKeyBindings`/`OnKeyDownNotHandled` on the current view,
before bubbling to `SuperView` and repeating; every one of those five steps, not just
`KeyBindings`, is evaluated at each ancestor level during that bubble). The override queries
whichever list is currently active (Decision 3) for an operation matching the pressed key, with no
assumption about which keys can appear:

```csharp
protected override bool OnKeyDownNotHandled(Key key)
{
    if (_shortcutSource.TabOperations.FirstOrDefault(h => h.Key == key) is { Action: { } action }) {
        action();
        return true;
    }

    return base.OnKeyDownNotHandled(key);
}
```

A list is free to expose any operation (a new key, or dropping one) via its own `TabOperations`
without the owning tab needing a matching edit — the tab has zero hardcoded knowledge of "the five
keys"; it just asks "does the active list have anything for this key" on every key that reaches it
unclaimed. Falling through to `base.OnKeyDownNotHandled(key)` (rather than a bare `return false`)
when the active list doesn't support the operation preserves the base `View`'s own
`KeyDownNotHandled` event-raising behavior and keeps the key genuinely unhandled for further
bubbling, matching how an un-bound key behaves everywhere else in the app today.

**Why the tab and not, say, `ManagementTabs`:** `openspec/specs/tab-content-structure/spec.md`
already requires each tab's content stay self-contained, with `MainWindow`/`ManagementTabs` only
resolving/constructing/registering. `ManagementTabs` has no knowledge of what's inside a tab's
content (StreamListView vs ConsumerListView vs SubscriptionsView, ...) — pushing dispatch there
would mean either a new cross-tab interface just to ask "what's your active list", or a switch on
tab type. The tab itself already knows its own active list.

**Alternative considered:** keep the list-level bindings *and* add tab-level ones as a fallback,
relying on Terminal.Gui to only reach the tab's binding once the list's own is absent from the
dispatch path. Rejected — it reintroduces exactly the "two things that must be kept in sync"
problem Goal 3 is trying to avoid (a list could support an op via `Shortcuts` without a matching
`KeyBindings` entry, or vice versa), for no behavioral benefit over single ownership.

**Alternative considered:** each tab `KeyBindings.Add`s a fixed set of keys it knows its levels use
(the original draft, see above) and dispatches through a small `Dispatch(Key)` helper. Rejected in
favor of `OnKeyDownNotHandled` — hand-declaring the key set duplicates information `TabOperations`
already carries (which keys exist is exactly what `TabOperations` enumerates), so a list gaining a
new operation would silently do nothing until someone remembered to also add a matching
`KeyBindings.Add` at every owning tab. `OnKeyDownNotHandled` has no such second place to forget.

### 2. `IShortcutSource.Shortcuts` splits into "list-local" and "tab-forwarded"

Today `DrillableListView<T>.Shortcuts` bundles all six possible hints (Refresh, Back, New,
Delete, Edit, Search) in one property. Post-move, the five operations that become tab-owned
(Refresh/New/Delete/Edit/Filter) must stop appearing in the *list's own* `IShortcutSource.Shortcuts`
— otherwise, when the list itself is the directly-focused view, `ShortcutAggregator.Collect`
would yield them once from the list (still an `IShortcutSource`) and again from the tab (which
also now advertises them), duplicating every entry in the Alt+K picker whenever the list has
direct focus.

So: `DrillableListView<T>.Shortcuts` keeps only Back (`Esc`, if `EnableAscend`) and Search (`/`,
if a `FilterBox` is attached) — the two operations that remain genuinely list-owned. The
Refresh/New/Delete/Edit/Filter hints move to a new small contract, `ITabOperationsSource`
(`internal interface ITabOperationsSource { IEnumerable<ShortcutHint> TabOperations { get; } }`,
alongside `IShortcutSource`), which `DrillableListView<T>` and `ListEditorView<T>` both implement
instead of folding those five into the public `IShortcutSource.Shortcuts`. It's for the owning tab
to read, not the aggregator — the tab's own `Shortcuts` is what the aggregator sees now for these
five (below). `KeyListView`/`ObjectListView`'s Ctrl+F append (`KeyListView.cs:37-38`) moves from
overriding `Shortcuts` to overriding `TabOperations` the same way.

The tab's own `Shortcuts` (satisfying `IShortcutSource` for the first time — none of today's tabs
implement it) becomes: `_shortcutSource.TabOperations`, where `_shortcutSource` (an
`ITabOperationsSource`) is whichever list is currently active (Decision 3). This is the same
collection `OnKeyDownNotHandled` reads from, so exposure and dispatch are provably in sync — a
hint only ever appears in the picker if invoking that same key would actually do something.

**Alternative considered:** leave `Shortcuts` untouched and instead have `ShortcutPickerDialog`/
`ShortcutAggregator` dedupe by key across the collected hints. Rejected — deduping papers over
two sources of truth instead of removing the second one, and does nothing for the dispatch side
(which needs its own single source regardless).

### 3. "Active list" is an explicit `SetShortcutSource` call at every level transition, not a computed ternary

Every drill-down tab already tracks which level is current via a private `string?
_currentStream`/`_currentBucket` field, and toggles two lists' `Visible` in lockstep with it
(`StreamsTab.Descend/Ascend`, `StreamsTab.cs:126-171`). An earlier draft computed "which list is
active" as a read-only property (`private ITabOperationsSource ActiveTabOperations =>
_currentStream is null ? _listView : _consumerListView;`) re-deriving the answer from
`_currentStream` on every read. This design instead adds a private `ITabOperationsSource
_shortcutSource` field plus a `private void SetShortcutSource(ITabOperationsSource source) =>
_shortcutSource = source;` method, called once in the constructor (the initial/top level) and once
more inside each of `Descend`/`Ascend`, right alongside the other per-level state that already
transitions there (`_details.SetActive(false); _consumerDetails.SetActive(HasFocus);
SetShortcutSource(_consumerListView);`). `OnKeyDownNotHandled` and `Shortcuts` both just read
`_shortcutSource` directly — no ternary, no re-derivation from `_currentStream` on every keystroke
or picker open. `SubscribeTab` has only one list, so it skips `_shortcutSource`/`SetShortcutSource`
entirely and reads `_subscriptionsView` directly in both places — there is no second level to ever
point it at.

**Why not a computed ternary:** it re-derives the same answer `Descend`/`Ascend` already know at
the exact moment they toggle every other piece of level state (`Visible`, `SetActive`, focus) —
computing it lazily elsewhere is one extra thing to keep in sync with those transitions rather
than a transition of its own. A shared base class or interface for "current level" was considered
and rejected for this change either way: three (soon four) tabs' worth of "point this at whichever
list is now active" is not enough duplication to justify a new `Components/` type, and
`tab-content-structure`'s "each tab owns its own internal layout" already leans against extracting
tab-internal state into something more general.

### 4. Ctrl+F stays exactly as scoped today — only its binding location moves

`KeyListView`'s Ctrl+F (server-side pre-fetch filter) and `ObjectListView`'s Ctrl+F (client-side
post-fetch filter) keep their distinct semantics and stay reachable only at the level(s) where
they exist today — bucket-level `BucketListView` never gains a Ctrl+F, `StreamsTab`/`SubscribeTab`
never do anything for it at all. `OnKeyDownNotHandled` already encodes this correctly for free,
with no per-tab knowledge that Ctrl+F is even a thing: if the active list's `TabOperations` has no
Ctrl+F entry, the lookup finds nothing, `OnKeyDownNotHandled` falls through to `base.
OnKeyDownNotHandled(key)`, and the key keeps bubbling unclaimed — exactly the same path any other
unsupported key takes.

### 5. `SubscribeTab`/`ListEditorView<T>` move too, despite the smaller functional gap

`SubscribeTab` has no sibling pane today, so `ListEditorView<T>`'s existing component-level
binding (`ListEditorView.cs:49-56`, deliberately already bound on "the whole component, not the
list" per its own comment) already covers every focus position inside `SubscribeTab`. Moving it
up to `SubscribeTab` itself has no *current* behavioral effect, but keeps all four tabs on one
pattern (Goal 4) — a future second focusable pane inside `SubscribeTab` gets the fix for free
instead of silently reopening this same gap. `PublishDialog`'s `HeaderEditorView` is the one
`ListEditorView<T>` usage that stays exactly as-is, since it has no owning tab.

## Risks / Trade-offs

- **[Risk] Terminal.Gui bubble-dispatch assumption is load-bearing but only verified against
  framework docs, not this app's actual `View` subclassing** (custom `OnKeyDown` overrides,
  `EditFrame`'s own focus handling, etc. could theoretically intercept first) → Mitigation: verify
  empirically via `tmux` (per `CLAUDE.md`) for at least one case per tab — Ctrl+N/E/R/F with focus
  on the attached `FilterBox` (a real sibling of the list, not a descendant of it, so this is the
  actual pre-existing gap this change closes), and again with focus on the list itself — before
  considering the implementation done; not just "it compiles." Note: `*Details` panes
  (`PollingDetailsView`) are `CanFocus = false` with no focusable children of their own, so despite
  earlier drafts of this design describing them as a reachable Tab stop, they are not one in the
  current UI — `FilterBox` is the concrete scenario that actually exercises this fix today.
- **[Risk] Splitting `Shortcuts` into list-local vs. tab-forwarded is a breaking change to an
  internal contract (`IShortcutSource`) two different base classes implement** → Mitigation: the
  `list-editor` and `drillable-list` spec deltas make the new split explicit so it's not just an
  implementation detail; `ListEditorView<T>.Shortcuts` for the one remaining standalone consumer
  (`HeaderEditorView`) is unaffected since that usage never had list-local-only hints to begin
  with (it has no Ctrl+R/Back/Search) — its full `Shortcuts` stays as today, unsplit, since it's
  never tab-hosted.
- **[Risk] `OnKeyDownNotHandled` is a less commonly reached-for override than `KeyBindings`, and
  its exact position in Terminal.Gui's per-view dispatch pipeline (after `KeyBindings`/
  `HotKeyBindings`, before bubbling to `SuperView`) is easy to get subtly wrong** → Mitigation:
  confirmed via the framework's own docs (`docs/keyboard.html`) that several of Terminal.Gui's own
  built-in views (`TextField`, `TextView`, `TableView`, `HexView`, `MenuBarItem`, `PopoverMenu`)
  already override it for the same "nothing more specific claimed this key" fallback role this
  design needs, and that the full `OnKeyDown`/`KeyDown`/`KeyBindings`/`HotKeyBindings`/
  `OnKeyDownNotHandled` sequence re-runs at every ancestor level as `NewKeyDownEvent`'s recursion
  into `Focused` unwinds — not just once at the originating view — so a tab-level override reaches
  exactly the same set of unclaimed keys a tab-level `KeyBindings.Add` would have.

## Migration Plan

No data/runtime migration — this is a pure UI key-handling refactor within a single release.
Land as one change (base classes + all four tabs together), since an intermediate state where
only some tabs have moved would leave the base classes' `Shortcuts` contract inconsistent between
tab-hosted and not-yet-migrated tabs. No rollback concerns beyond normal revert.

## Open Questions

- None outstanding — the one open question from the proposal stage (Terminal.Gui key-dispatch
  order relative to ancestor vs. focused-descendant `KeyBindings`) is resolved by Decision 1/the
  framework docs cited in Context; the `tmux`-based empirical check in Risks is verification, not
  a design unknown.
