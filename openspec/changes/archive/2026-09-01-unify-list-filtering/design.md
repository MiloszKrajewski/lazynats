## Context

Two list bases exist: `DrillableListView<T>` (Streams/Consumers, Buckets ×2, Keys, Objects) and
`ListEditorView<T>` (Subscriptions, Publish headers). `DrillableListView<T>` already has a shared,
opt-in quick-search shape (`AttachFilterBox`/`IFilterable`, fuzzy-subsequence match via
`RegexExtensions.FuzzyToRegex`) — every `DrillableListView<T>` subclass already uses it. What's
missing is a second, *sticky* filter layer using the stricter `* ? >` grammar
(`Core/KeyFilterExpression.cs`), today wired ad hoc:

- `KeyListView`/`ValuesTab`: Ctrl+F opens `PatternDialog`, `ValuesTab` compiles the pattern via
  `KeyFilterExpression.TryCompile`, uses `.NativeFilter` to scope `store.GetKeysAsync([...])`
  server-side, and applies `.PostFilter` (nullable) client-side only when the native filter alone
  isn't exact. Capped at 10,000 matches.
- `ObjectListView`/`ObjectsTab`: Ctrl+F opens the same `PatternDialog`, but `ObjectsTab` matches
  with `RegexExtensions.WildcardToRegex` — a different, weaker grammar (case-insensitive, `*`/`?`
  only, full-string glob) — applied to a full unscoped fetch (Object Store has no server-side
  name-wildcard fetch API).
- `StreamListView`, `ConsumerListView`, `BucketListView`: no Ctrl+F at all.
- `SubscriptionsView`, `HeaderEditorView` (`ListEditorView<T>`): no filtering of any kind.

The object store's lack of native scoping is exactly the case the proposal wants to generalize:
most lists (Streams, Consumers, Buckets, Objects, headers) can never scope their fetch, so for them
the compiled expression's regex is the *entire* filtering story — there is no "native filter"
concept to reason about at all. Only KV keys can scope a fetch server-side.

`SubscriptionsView` stays out of scope for both shapes (see Decision 5): its rows are themselves
subject-pattern filters over the live feed, not names to narrow a list by, so neither shape earns
its keep there.

## Goals / Non-Goals

**Goals:**
- One grammar (`* ? >`, per `kv-filter-expression`) and one compiled-regex code path for every
  Ctrl+F pattern filter in the app.
- One shared, opt-in wiring shape per list base (`DrillableListView<T>.EnableFilter()`,
  `ListEditorView<T>`'s equivalent) so a subclass activates Ctrl+F filtering the same way it
  activates Create/Delete/Edit — no per-tab dialog/event boilerplate for the common (non-native)
  case.
- Preserve KV keys' existing server-scoped-fetch behavior and its exact spec (`nats-kv`'s
  "Server-Side Key Filter") byte-for-byte; it is the one genuine special case, not the template.
- Bring quick-search and Ctrl+F filtering to every list that currently lacks one and would actually
  benefit from it, including `ListEditorView<T>`'s header editor.

**Non-Goals:**
- Changing quick-search's fuzzy-subsequence matching semantics (unaffected by this change).
- Adding *new* server-side scoping capability anywhere filtering doesn't already have it (Streams,
  Consumers, Buckets, Objects, headers stay full-fetch-then-filter; only the matching grammar and
  the affordance are unified).
- Changing the 10,000-match cap or its KV-specific truncation-indicator UI.
- Adding either shape to `SubscriptionsView` (see Decision 5) — its rows are filter expressions,
  not names, so this doesn't apply there.

## Decisions

### Decision 1: Split the filter engine into an always-available regex and an optional native filter

`Core/KeyFilterExpression.cs` becomes the shared engine (kept in `Core/`, renamed to reflect its
broadened scope — e.g. `FilterExpression`). Its compiled result keeps exposing a native NATS
subject-filter string (still derived exactly as today, over-approximating where needed) *and* now
unconditionally also exposes the exact-match `Regex` — including in what's today the "native fast
path", where `PostFilter` is currently `null`. Concretely:

```
record CompiledFilter(string NativeFilter, Regex Regex, bool NativeFilterIsExact)
```

- `NativeFilterIsExact` is `true` exactly when today's fast path applies (expression is already
  valid native NATS syntax) — a consumer that scopes its fetch by `NativeFilter` can skip applying
  `Regex` afterward when this is `true`, purely as a perf optimization (KV keys' current
  behavior, unchanged).
- Every other consumer ignores `NativeFilter`/`NativeFilterIsExact` entirely and just applies
  `Regex` to whatever it already has in memory — no over-approximation semantics leak into them,
  because they never had a server-scoped fetch to over-approximate for in the first place.

This is the "parametrization" the proposal asks for: whether native scoping happens is a decision
made once, at the one call site that has a native fetch to scope (`ValuesTab`'s key-list refresh);
everywhere else, "parametrized off" simply means that code path is never reached.

**Alternative considered**: keep `PostFilter` nullable and have non-native consumers special-case
"null means match everything" (mirroring today's KV logic literally). Rejected — it forces every
non-native consumer to re-derive "when is PostFilter null and why is that still exact for me"
reasoning that only actually holds for a native-scoped fetch; always returning a real `Regex` is
simpler and correct unconditionally.

### Decision 2: `DrillableListView<T>.EnableFilter()` owns sticky in-memory filtering itself

Unlike Create/Delete/Edit (which only ever raise an event for the owning tab to handle), the new
`EnableFilter()` shape is *not* a thin event pass-through. It owns the whole non-native case
end-to-end inside the base class:

- Binds Ctrl+F, opens `PatternDialog` (validator = `FilterExpression.TryCompile(...) is not null`),
  seeded with the current sticky pattern.
- On confirm, compiles the pattern, stores it as sticky filter state (a second predicate layered
  with quick-search's existing fuzzy predicate — both apply as an AND when both are active,
  matching `nats-kv`'s existing "both may be active at once" contract), and re-derives `_filtered`.
- Unlike quick-search's text (which resets on every `ReplaceItems`, per `drillable-list`'s existing
  "Search text resets on refresh" requirement), the sticky pattern survives `ReplaceItems` — it only
  clears when the owning tab explicitly calls a new `ClearFilter()` (mirroring today's per-tab
  `_current*Filter = null` resets on descend/ascend).
- Exposes the active pattern (for a tab's title/header text) and raises a `FilterChanged(string?
  pattern)` event.

A tab that only needs "filter what's already loaded" (Streams, Consumers, Buckets, Objects)
activates `EnableFilter()` and does nothing else — no dialog code, no event handler, matching how
`EnableCreate()`/`EnableDelete()` already work. `ValuesTab` is the one tab that additionally
subscribes to `FilterChanged` to *also* re-scope its server-side key fetch (using the compiled
expression's `NativeFilter`) and enforce the 10,000-match cap — the base class's own in-memory
`Regex` application still runs afterward and is harmless (a no-op narrowing) when the native fetch
was already exact, and is the actual filtering mechanism when it wasn't.

**Alternative considered**: keep `EnableFilter()` a thin event-raiser like Create/Delete/Edit, and
make every tab (including Streams/Consumers/Buckets/Objects) explicitly own "compile pattern → set
state → intersect with quick-search → re-derive `_filtered`" itself. Rejected — that's exactly the
boilerplate duplication (`OpenKeyFilterDialog`/`OpenObjectFilterDialog`, near-identical today) the
proposal exists to eliminate; only the one genuinely special case (KV keys' server fetch) still
needs tab-level code.

### Decision 3: `ObjectListView` moves onto the shared shape, dropping its own filter path

`ObjectListView` activates `EnableFilter()` like Streams/Consumers/Buckets (no `FilterChanged`
subscription — Object Store still can't scope a fetch). Its existing `FilterRequested` event and
`ObjectsTab.OpenObjectFilterDialog`/`RegexExtensions.WildcardToRegex` call site are removed.
`ObjectsTab.RefreshObjectListAsync` keeps re-fetching the full name list on every trigger (Ctrl+R,
upload, delete) exactly as today — `EnableFilter()`'s in-memory regex narrows whatever
`ReplaceItems` hands it, same as quick-search already does, so no behavior changes there beyond the
matching grammar itself.

This is the one user-visible **BREAKING** change: object filter patterns become case-sensitive and
gain NATS subject-token semantics (`*` doesn't cross `.`, `>` is now meaningful) instead of a plain
case-insensitive glob. `RegexExtensions.WildcardToRegex` becomes dead code and is deleted along
with this change (its only other caller, `FuzzyToRegex`, stays — it backs quick-search everywhere
and is untouched).

### Decision 4: `ListEditorView<T>` adopts an items/filtered split to gain both shapes

`ListEditorView<T>` currently has no filtered view — `Add`/`Replace(index, ...)`/`Delete(index)`
all index directly into `_items`, and `ListView.SelectedItem` is that same raw index.
`DrillableListView<T>`'s `_items`/`_filtered` split (with `GetIdentity`) doesn't directly apply
here — `ListEditorView<T>` has no identity accessor and orders items by insertion, not
alphabetically (subscriptions and headers are user-ordered lists, not sorted collections).

Instead: `ListEditorView<T>` gains its own `_filtered` `ObservableCollection<T>` (a live view over
`_items`, same relative order, narrowed by the AND of quick-search's fuzzy predicate and the sticky
`* ? >` predicate — both shapes reuse the exact same `FilterBox`/`PatternDialog`/`FilterExpression`
plumbing `DrillableListView<T>` uses). The `ListView` binds to `_filtered`. `TryEditItem`/
`TryDeleteItem` resolve the selected *filtered* index back to its position in `_items` (a direct
`IndexOf` on the reference/value — item identity isn't needed since these lists don't reorder on
their own) before calling the existing `Replace(index, ...)`/`Delete(index)` overridables, which
keep operating on `_items` unchanged. `TryCreateItem`'s `Add` is unaffected (appends to `_items`;
the filtered view picks it up via the collection-changed re-derive, same as quick-search already
does for `DrillableListView<T>`).

**Alternative considered**: give `ListEditorView<T>` an identity accessor and reuse
`DrillableListView<T>`'s exact split, or extract a common base. Rejected as unnecessary churn —
`ListEditorView<T>` doesn't sort and doesn't need identity-preserving replace (its collection only
ever changes via Add/Replace/Delete, never a wholesale `ReplaceItems`), so the two bases' filtering
mechanics are similar in shape but don't share enough to justify a new common ancestor right now.

### Decision 5: Where `EnableFilter()`/quick-search get activated

| List | Quick-search (`/`) | Ctrl+F filter | Native fetch scoping |
|---|---|---|---|
| `StreamListView` | already on | **new** | no |
| `ConsumerListView` | already on | **new** | no |
| `Values/BucketListView`, `Objects/BucketListView` | already on | **new** | no |
| `KeyListView` | already on | already on (kept, via shared engine) | yes (unchanged) |
| `ObjectListView` | already on | swapped to shared grammar | no |
| `HeaderEditorView` | **new** | **new** | no |
| `SubscriptionsView` | **excluded** | **excluded** | n/a |

`SubscriptionsView` is the one `ListEditorView<T>` instance deliberately left out. Its rows aren't
names being searched for — each one *is itself* a subject-pattern filter already applied to the
live feed. Narrowing "which filter expressions look like X" doesn't map onto either shape's actual
use case (finding an item among many by a fragment of its name), and the list is typically short
enough that it doesn't need narrowing at all. `HeaderEditorView`'s rows (`Key: Value` pairs) don't
have this problem — a header's key is a name being looked up, not a filter — so it keeps both
shapes.

### Decision 6: Filtered and refreshed results are always alphabetically sorted, never server order

`DrillableListView<T>.ReplaceItems` already sorts `_items` ascending by `GetIdentity` (Ordinal)
unconditionally — regardless of what order the caller handed it in — per `drillable-list`'s
existing "Default Alphabetical Ordering" requirement, and `ApplyFilterAndSelect` derives
`_filtered` by iterating that already-sorted `_items`, so quick-search results are alphabetical
today too. This invariant is preserved, not just by accident but by construction, for every new
filter path this change adds:

- `EnableFilter()`'s sticky predicate (Decision 2) is layered as another `.Where()` over the same
  already-sorted `_items` iteration `ApplyFilterAndSelect` already uses — it narrows, it never
  reorders.
- The one native-scoped fetch (KV keys, Decision 1) hands its (server-ordered) results straight to
  the existing `ReplaceItems`, which re-sorts them exactly as it already does for an unfiltered
  fetch — server order never reaches the screen.
- `ObjectsTab`'s re-fetch-on-filter-change (Decision 3) goes through the same `ReplaceItems` call
  it already used before this change, so it was — and remains — sorted despite Object Store's
  `ListAsync` making no ordering guarantee of its own.
- `ListEditorView<T>`'s new `_filtered` (Decision 4) deliberately does *not* introduce alphabetical
  sorting — that base was never sorted (subscriptions/headers are user-ordered, not fetched from a
  server), and this change doesn't touch that; the "no server order" concern doesn't apply to it.

No task in this change needs to add new sorting logic — the risk is purely regression during the
refactor (e.g. a native-scoped consumer trusting fetch order instead of going through
`ReplaceItems`), which task 1.2 and 6.3 (tasks.md) explicitly guard against.

## Risks / Trade-offs

- [Object filter grammar change breaks existing muscle memory/saved habits] → Called out as
  **BREAKING** in the proposal; the new grammar is strictly more expressive (adds `>`, exact `?`),
  so most existing simple patterns (`foo*`, `*bar`) keep working, just case-sensitively.
- [`ListEditorView<T>`'s filtered-index translation is new code, unlike `DrillableListView<T>`'s
  already-proven split] → Keep the translation minimal (direct list `IndexOf`, no identity
  abstraction) and cover it with the same scenario shapes `drillable-list`'s quick-search
  requirements already use, adapted for `list-editor`.
- [Adding Ctrl+F to `BucketListView` affects two tabs (Values and Objects) at once] → Both tabs
  already share this exact view class for quick-search today with no divergence; the new shape
  follows the same shared-component contract.

## Open Questions

- None — Decisions 1-5 above resolve the parametrization question the proposal raised.
