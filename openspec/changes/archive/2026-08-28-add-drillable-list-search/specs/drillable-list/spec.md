## ADDED Requirements

### Requirement: Default Alphabetical Ordering
A drillable list SHALL present its items in ascending alphabetical order by identity (per the
overridable identity accessor), applied whenever the item collection is replaced. This ordering is
the base's default behavior for every subclass and requires no subclass opt-in.

#### Scenario: Replaced items are sorted by identity
- **WHEN** the item collection is replaced with items whose identities are not already in
  alphabetical order
- **THEN** the list displays them in ascending alphabetical order by identity, regardless of the
  order they were supplied in

#### Scenario: Ordering applies without any subclass opt-in
- **WHEN** a drillable list subclass activates none of the shared opt-in shapes beyond its
  required navigation
- **THEN** its items are still displayed in ascending alphabetical order by identity

### Requirement: Shared Quick-Search Wiring
A drillable list SHALL offer a shared, opt-in quick-search shape: when activated by a subclass,
pressing `/` while the list holds focus SHALL focus a persistent, always-visible search field
associated with that list, and a "Search" hint SHALL appear among the list's shortcut hints. Text
entered into the field SHALL filter the currently-loaded items live, in memory, without issuing any
fetch or raising `RefreshRequested`. Filtering SHALL use case-insensitive fuzzy-subsequence
matching against each item's identity: query text `q` matches an item whose identity contains every
character of `q`, in the same relative order, with any (including zero) characters in between —
equivalently, `q` behaves as if a wildcard were inserted between each of its characters (e.g. `oce`
matches as `*o*c*e*`). A filtered result set SHALL remain in the same ascending alphabetical order
as the unfiltered list, since matching is a pass/fail predicate, not a relevance ranking.

#### Scenario: Activating quick-search wiring raises no effect until the field is used
- **WHEN** a subclass activates the shared quick-search wiring and the user has not yet entered any
  text into the search field
- **THEN** the list displays all currently-loaded items, in alphabetical order, exactly as it would
  without quick-search activated

#### Scenario: Non-contiguous characters match
- **WHEN** the search field contains `oce` and an item's identity is `OperationCancelledException`
- **THEN** that item remains visible in the filtered list, since `o`, `c`, and `e` occur in that
  relative order within the identity, even though they are not contiguous

#### Scenario: A non-matching query hides an item
- **WHEN** the search field contains text whose characters do not occur, in order, anywhere within
  an item's identity
- **THEN** that item is not shown in the filtered list

#### Scenario: Matching is case-insensitive
- **WHEN** the search field contains lowercase text and an item's identity contains the same
  characters in a different case, in the matching relative order
- **THEN** that item is shown in the filtered list

#### Scenario: Filtered results stay alphabetically ordered
- **WHEN** the search field's text matches more than one item
- **THEN** the matching items are displayed in the same ascending alphabetical order they would
  appear in unfiltered, not reordered by match quality

#### Scenario: Clearing the search field restores the full list
- **WHEN** the search field's text is cleared to empty
- **THEN** the list displays every currently-loaded item again, in alphabetical order

#### Scenario: Quick-search filtering never triggers a fetch
- **WHEN** the user types into or clears the search field
- **THEN** the list does not raise `RefreshRequested` and no data is re-fetched from the server —
  only the already-loaded items are filtered

#### Scenario: Esc clears non-empty search text before affecting anything else
- **WHEN** the search field holds focus, contains non-empty text, and the user presses Esc
- **THEN** the search field's text is cleared, the full (unfiltered) list is restored, and focus
  returns to the list

#### Scenario: Esc on an already-empty search field falls through to ascend
- **WHEN** the search field holds focus, is already empty, and the user presses Esc, on a list that
  has also activated the shared ascend wiring
- **THEN** the same result as pressing Esc directly on the list (ascend) occurs

#### Scenario: Enter moves focus from the search field into the list
- **WHEN** the search field holds focus and the user presses Enter
- **THEN** focus moves to the list, and the search field's text and the filtered view are left
  unchanged

#### Scenario: Search text resets on refresh
- **WHEN** the item collection is replaced (via `Ctrl+R` or any other refresh)
- **THEN** the search field's text is cleared back to empty and the full, newly-replaced item
  collection is shown

#### Scenario: A subclass that does not activate quick-search has no search affordance
- **WHEN** a subclass activates none of the shared shapes including quick-search
- **THEN** `/` has no effect on that list, no search field is present, and no "Search" hint appears
  among its shortcut hints

### Requirement: Nearest-Identity Lookup
A drillable list SHALL expose an operation that, given an identity no longer present in the current
(alphabetically ordered) collection, returns the identity of the nearest remaining item by sort
order — the item that would immediately precede or follow the given identity were it still present
— or `null` if the collection is empty. This is distinct from the existing index-adjacency
`NeighborIdentity` lookup, which operates on list position rather than sort order.

#### Scenario: Nearest identity between two remaining items
- **WHEN** the nearest-identity lookup is called with an identity that would sort between two items
  still present in the collection
- **THEN** the identity of one of those two neighboring items is returned

#### Scenario: Nearest identity when the target would sort before everything remaining
- **WHEN** the nearest-identity lookup is called with an identity that would sort before every item
  currently in the collection
- **THEN** the identity of the first (alphabetically earliest) remaining item is returned

#### Scenario: Nearest identity when the target would sort after everything remaining
- **WHEN** the nearest-identity lookup is called with an identity that would sort after every item
  currently in the collection
- **THEN** the identity of the last (alphabetically latest) remaining item is returned

#### Scenario: Nearest identity on an empty collection
- **WHEN** the nearest-identity lookup is called while the collection is empty
- **THEN** `null` is returned

## MODIFIED Requirements

### Requirement: Identity-Preserving Replace
A drillable list SHALL expose an operation that replaces its entire item collection wholesale and
restores the highlight to the item whose identity (per the overridable identity accessor) matches
the previously-highlighted item, if that identity is still present in the new collection;
otherwise the nearest remaining item by sort order (per "Nearest-Identity Lookup") SHALL become
highlighted, or no item if the new collection is empty.

#### Scenario: Replace preserves the highlight when possible
- **WHEN** the item collection is replaced and an item with the same identity as the
  previously-highlighted item is present in the new collection
- **THEN** that item becomes highlighted

#### Scenario: Replace falls back to the nearest remaining item
- **WHEN** the item collection is replaced and no item in the new collection shares the
  previously-highlighted item's identity
- **THEN** the item nearest to the previously-highlighted identity by sort order becomes
  highlighted, provided the new collection is non-empty

#### Scenario: Replace with an empty collection clears the highlight
- **WHEN** the item collection is replaced with an empty collection
- **THEN** no item is highlighted

#### Scenario: A quick-search keystroke that removes the highlighted item falls back the same way
- **WHEN** a list has activated the shared quick-search wiring, an item is highlighted, and the
  user types text into the search field that excludes that item from the filtered view
- **THEN** the nearest remaining item by sort order within the filtered view becomes highlighted,
  the same fallback "Replace falls back to the nearest remaining item" describes
