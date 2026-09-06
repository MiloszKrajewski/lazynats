## Why

Production NATS state (KV keys, object names, streams, consumers) can grow well past what a user
can scan by eye, and today every `DrillableListView<T>` subclass shows items in whatever order the
server happened to return them, with no way to narrow the view except scrolling. Finding one
specific item — "inspect this key," "delete that consumer" — becomes a manual hunt instead of a
lookup. This change makes every drillable list sortable and searchable over whatever is already
loaded, independent of and prerequisite to a separate, larger effort to shrink what gets fetched
from the server in the first place for very large KV buckets (deferred — see Impact).

## What Changes

- Every `DrillableListView<T>` subclass (`StreamListView`, `ConsumerListView`, the KV and OBJ
  `BucketListView`s, `KeyListView`, `ObjectListView`) presents its items in ascending alphabetical
  order by identity, instead of today's implicit fetch/arrival order.
- Drillable lists gain an opt-in quick-search affordance (mirroring the existing
  `EnableDescend`/`EnableAscend`/`EnableCreate`/... shared-shape pattern): a persistent, always-
  visible search field whose text filters the currently loaded items live, in memory, with no
  network round-trip. Matching is case-insensitive fuzzy-subsequence ("`oce`" matches any item
  containing `o`, then `c`, then `e`, in that order, anywhere — equivalent to typing
  `*o*c*e*`), not literal substring and not scored/ranked, so a filtered result set stays in the
  same alphabetical order as the unfiltered list.
- When the currently highlighted item is no longer present in the (possibly filtered) view — most
  commonly because a quick-search keystroke just excluded it — the list highlights the nearest
  remaining item in sort order (via a binary-search-style lookup) instead of jumping to the top of
  the list.
- Out of scope: nothing about what gets fetched from the server changes. A KV bucket with a very
  large key count still fetches unconditionally today; this change only improves how the already-
  fetched set is displayed and searched. Narrowing the fetch itself (a NATS-native pre-fetch
  pattern, a fetch cap, a truncation indicator) is intentionally deferred to a follow-up change.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `drillable-list`: adds default alphabetical ordering, an opt-in quick-search shared shape (new
  keybinding, hint, and always-visible field, alongside the existing opt-in shapes), and a
  nearest-identity lookup (sort-order based, distinct from the existing index-adjacency
  `NeighborIdentity`) used to keep a sensible highlight when the current item drops out of view.

## Impact

- `src/lazynats/Components/DrillableListView.cs`: add sorting, the opt-in quick-search field/
  wiring, and a `NearestIdentity`-style lookup alongside the existing `NeighborIdentity`.
- Each subclass (`Streams/StreamListView.cs`, `Streams/ConsumerListView.cs`,
  `Values/BucketListView.cs`, `Values/KeyListView.cs`, `Objects/BucketListView.cs`,
  `Objects/ObjectListView.cs`) opts into the new sort/search shape.
- No change to any `*Tab.cs` fetch/refresh logic (`ValuesTab.RefreshKeyListAsync`,
  `ObjectsTab`'s equivalent, `StreamsTab`, ...) — items are still fetched exactly as they are
  today; only presentation changes.
- No new dependencies: fuzzy-subsequence matching is a small hand-written scan, not a library, to
  stay `PublishAot`-friendly.
- Explicitly not addressed here (tracked as a follow-up): KV's pre-fetch NATS pattern, a fetch
  cap, and a truncation indicator for buckets too large to fetch in full.
