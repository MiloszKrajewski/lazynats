## Why

Filtering exists today, but inconsistently: KV keys have both an in-memory fuzzy quick-search
(`/`) and a server-scoped `* ? >` pattern filter (Ctrl+F); Object names have the same quick-search
but a *different*, weaker Ctrl+F grammar (case-insensitive filesystem globbing, no `>`); Streams,
Consumers, and both Bucket lists (Values and Objects) have quick-search but no Ctrl+F pattern
filter at all; and the Publish dialog's header list has neither.
Every list is either a `DrillableListView<T>` or a `ListEditorView<T>`, and both mechanisms are
proven — they just haven't been extended to every instance, and the pattern-filter grammar itself
has drifted into two incompatible dialects. Users managing larger NATS deployments (many streams,
consumers, buckets, keys) need the same fast-narrowing tools everywhere, not just on KV keys. The
Subscribe tab's list is deliberately excluded: each row there already *is* a subject-pattern
filter over the live feed, not a name to narrow down by — searching or filtering "which filters
look like X" doesn't carry its weight the way it does for a list of names.

## What Changes

- Generalize the KV key filter grammar (`kv-filter-expression`'s `* ? >` compiler) into a shared
  engine that always produces an exact client-side regex, and separately exposes a native
  NATS-subject filter string only for the one consumer that can actually use it (KV keys'
  server-scoped fetch) — every other list uses the regex alone, with no over-approximation and no
  server-side scoping attempted.
- Add a shared, opt-in "Filter" (Ctrl+F) shape to `DrillableListView<T>`, alongside the existing
  Create/Delete/Edit/Ascend shapes, so a subclass activates sticky `* ? >` pattern filtering (that
  persists across Ctrl+R, unlike quick-search) with one call instead of hand-rolling its own dialog
  and event, as `KeyListView`/`ObjectListView` do today.
- Activate that shape on `StreamListView`, `ConsumerListView`, and `BucketListView` (used by both
  the Values and Objects tabs), giving Streams, Consumers, and Buckets a Ctrl+F pattern filter for
  the first time.
- Re-point `ObjectListView`'s existing Ctrl+F filter at the shared grammar, retiring its
  filesystem-glob-based matching (`*`/`?` only, case-insensitive, substring-anchored). **BREAKING**:
  object name filter patterns are now case-sensitive and use NATS subject-token semantics (`*`
  stops at `.`; `>` is supported) instead of a plain glob — existing muscle-memory patterns like
  `*foo*` may no longer match the same names.
- Extend `ListEditorView<T>` with the same two shapes (quick-search `/`, sticky Ctrl+F), requiring
  it to adopt an items/filtered-view split like `DrillableListView<T>` already has, so its
  index-based Add/Replace/Delete keep working under an active filter.
- Activate both shapes on `HeaderEditorView` (the Publish dialog's header list), bringing it to
  parity with every other list. `SubscriptionsView` (Subscribe tab's subject-pattern list) is the
  one `ListEditorView<T>` instance that deliberately does *not* activate either shape — its rows
  are themselves filter expressions over the live feed, not names to search/filter by.

## Capabilities

### New Capabilities
- `list-filter-affordance`: the shared quick-search (`/`, fuzzy) and pattern-filter (Ctrl+F,
  `* ? >`) shapes available uniformly across every list-bearing view, and the rule for when a
  filter additionally scopes a server-side fetch (only where the backing store supports native
  subject-wildcard fetching) versus applying as a pure in-memory regex everywhere else.

### Modified Capabilities
- `drillable-list`: add a shared, opt-in "Filter" (Ctrl+F) wiring shape, mirroring the existing
  Create/Delete/Edit shapes.
- `list-editor`: add quick-search and Filter (Ctrl+F) shapes, and the filtered-view mechanics
  needed for its index-based commit operations to keep working under a filter.
- `nats-obj`: "Post-Fetch Object Name Filter" changes from case-insensitive filesystem wildcards to
  the shared case-sensitive `* ? >` grammar. **BREAKING**
- `nats-streams`: Stream List and Consumer List each gain a Ctrl+F pattern-filter requirement.
- `nats-kv`: Bucket List gains a Ctrl+F pattern-filter requirement (Key List's existing server-side
  filter is unaffected).
- `nats-publish`: the header list gains both quick-search and Ctrl+F pattern-filter requirements.

## Impact

- `src/lazynats/Core/KeyFilterExpression.cs` (renamed `FilterExpression.cs`) generalizes beyond KV;
  `nats-obj`'s `RegexExtensions.WildcardToRegex` call site is removed in favor of it.
- `src/lazynats/Components/DrillableListView.cs`, `Components/ListEditorView.cs`,
  `Components/FilterBox.cs` gain the new shared shape(s).
- `src/lazynats/Streams/StreamListView.cs`, `Streams/ConsumerListView.cs`,
  `Values/BucketListView.cs`, `Objects/BucketListView.cs` (two separate, near-identical classes,
  not one shared component — see design.md), `Objects/ObjectListView.cs`,
  `Publish/HeaderEditorView.cs` and their owning tabs pick up the new wiring.
- No server/protocol impact — everything here is client-side list presentation.
