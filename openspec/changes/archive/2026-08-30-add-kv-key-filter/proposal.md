## Why

Production KV buckets can hold far more keys than the Values tab can fetch and display
comfortably — `ValuesTab.RefreshKeyListAsync` currently calls `store.GetKeysAsync()`
unconditionally, pulling every key in the bucket on every descend, Ctrl+R, and post-create/edit
refresh. `add-drillable-list-search` (archived) added in-memory fuzzy search over whatever is
already loaded, but explicitly deferred the actual fetch-size problem and reserved Ctrl+F for it
("earlier discussion of the (separate, deferred) KV pre-fetch-pattern change reserved for that
dialog, to avoid the two changes colliding on the same key later" — design.md Decision 5). This
change delivers that deferred piece: a way to narrow what gets fetched from the server in the
first place, using NATS's own subject-wildcard filtering, before a large bucket becomes unusable
to browse.

## What Changes

- The key-level list in the Values tab (`KeyListView`) gains a Ctrl+F binding that opens a
  single-field pattern dialog (reusing `PatternDialog`, extended to allow an empty/cleared value)
  seeded with the currently active filter, if any.
- Confirming the dialog re-fetches the key list scoped to that pattern via
  `INatsKVStore.GetKeysAsync(IEnumerable<string> filters, ...)` — a NATS subject-wildcard filter
  evaluated server-side — instead of always fetching every key. An empty pattern clears the
  filter (fetches all keys, today's behavior).
- The active filter (if any) persists across Ctrl+R and any other same-bucket refresh (create,
  edit), so narrowing a huge bucket down to a usable subset isn't undone by the next refresh; it
  resets to "no filter" on ascend/descend (leaving or re-entering a bucket).
- The key list's title/hint reflects an active filter so it's visually obvious the list isn't
  showing every key in the bucket.
- Out of scope: a fetch cap and truncation indicator for buckets too large to fetch even when
  filtered (`add-drillable-list-search`'s proposal.md names these as further, still-deferred
  follow-ups). Object store listing has no equivalent server-side filter in
  `NATS.Client.ObjectStore` 2.8.2, so `ObjectListView`/Objects tab are unaffected.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `nats-kv`: adds a server-side key-filter affordance (Ctrl+F) at the key level, changes "Key
  List"/"Manual Key List Refresh" to fetch scoped to the active filter pattern instead of always
  fetching every key, and defines the filter's lifetime across refresh vs. ascend/descend.

## Impact

- `src/lazynats/Values/KeyListView.cs`: bind Ctrl+F, raise a filter-requested event, add a
  "Filter" shortcut hint.
- `src/lazynats/Values/ValuesTab.cs`: track the active filter pattern per drilled-into bucket,
  open the pattern dialog on the new event, pass the pattern into `RefreshKeyListAsync`, update
  the list title/hint to show it.
- `src/lazynats/Subscriptions/PatternDialog.cs`: allow an empty confirmed value behind an
  opt-in flag, without changing its existing (non-empty-required) behavior for subscription
  patterns.
- No change to `DrillableListView<T>` — this is `KeyListView`-specific, not a new shared opt-in
  shape (no other current list has a server-side-filterable fetch to hang one off of).
