## ADDED Requirements

### Requirement: Shared Filter Wiring
A drillable list SHALL offer a shared, opt-in "Filter" affordance, independent of the shared
quick-search wiring: when activated by a subclass, the list exposes a filter operation — opening a
modal pattern dialog when invoked, with a "Filter" label — among the operations its owning tab can
dispatch Ctrl+F to per `tab-scoped-list-shortcuts`, without the subclass needing to build the
dialog, compile the pattern, or apply it itself. The dialog SHALL be seeded with the currently
active filter pattern, or empty if none is active, and SHALL reject an invalid pattern per the
filter-expression grammar (see `list-filter-affordance`). On a valid, non-empty confirmation the
list SHALL compile the pattern and narrow its currently-loaded items to matches; on an empty
confirmation the active filter SHALL be cleared; cancelling (Esc) SHALL leave the active filter (or
lack of one) unchanged.

#### Scenario: Activating filter wiring surfaces a Filter operation
- **WHEN** a subclass activates the shared filter wiring
- **THEN** a "Filter" operation is present among the operations this list exposes to its owning tab,
  invocable via Ctrl+F while this list is the active list

#### Scenario: Confirming a valid, non-empty pattern narrows the list
- **WHEN** the user opens the filter dialog, enters a valid, non-empty pattern, and confirms
- **THEN** that pattern becomes the list's active filter, and only currently-loaded items matching
  it (per the filter-expression grammar) remain shown

#### Scenario: Confirming an empty pattern clears an active filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared and every currently-loaded item is shown again (subject
  only to quick-search, if also active)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: A list that does not activate filter wiring has no Filter operation
- **WHEN** a subclass activates none of the shared shapes including filter wiring
- **THEN** Ctrl+F has no effect on that list, and no "Filter" operation is present among the
  operations it exposes to its owning tab

### Requirement: Active Filter Persists Across a Refresh
Unlike the shared quick-search wiring's search text (which resets on every `ReplaceItems`), an
active filter set via the shared filter wiring SHALL survive `ReplaceItems` unchanged, continuing
to narrow whatever the replaced item collection now holds. The active filter SHALL clear only when
the owning tab explicitly invokes a `ClearFilter` operation the base class exposes for this
purpose — e.g. when the currently-filtered scope itself changes, such as descending into a
different bucket or stream.

#### Scenario: The filter persists across a plain refresh
- **WHEN** a filter is active and the list's item collection is replaced (e.g. via Ctrl+R)
- **THEN** the refreshed items are immediately narrowed by the still-active filter, rather than the
  filter being reset

#### Scenario: ClearFilter resets the active filter
- **WHEN** the owning tab invokes `ClearFilter`
- **THEN** the active filter is cleared and every currently-loaded item is shown again (subject
  only to quick-search, if also active)

### Requirement: Filter and Quick-Search Combine When Both Are Active
When a list has activated both the shared quick-search wiring and the shared filter wiring, and
both currently hold non-empty state, an item SHALL be shown only if it matches both the active
filter pattern and the active quick-search query.

#### Scenario: An item matching only one of the two is hidden
- **WHEN** both a filter and a quick-search query are active, and an item's identity matches only
  one of the two
- **THEN** that item is not shown

#### Scenario: An item matching both is shown
- **WHEN** both a filter and a quick-search query are active, and an item's identity matches both
- **THEN** that item is shown

### Requirement: Filter Changes Are Observable For Optional Server-Side Scoping
A drillable list with the shared filter wiring active SHALL raise a notification whenever the
active filter pattern changes (including to/from cleared), carrying the new pattern (or none),
purely as an optional hook — an owning tab MAY subscribe to it to additionally scope a server-side
fetch (see `list-filter-affordance`'s "Server-Side Fetch Scoping Is an Additive Optimization, Not a
Prerequisite"), but the list's own in-memory narrowing (per "Shared Filter Wiring") functions
correctly whether or not any tab subscribes.

#### Scenario: The notification fires on every confirm and clear
- **WHEN** the filter dialog is confirmed with a new pattern, or confirmed empty to clear the
  filter
- **THEN** the filter-changed notification is raised carrying the corresponding new pattern (or
  none)

#### Scenario: A tab that does not subscribe is unaffected
- **WHEN** an owning tab does not subscribe to the filter-changed notification
- **THEN** the list still narrows its currently-loaded items to matches exactly as described in
  "Shared Filter Wiring", with no missing behavior

### Requirement: Filtered Results Stay Alphabetically Ordered, Regardless Of Fetch Order
Narrowing the list via the shared filter wiring (alone, or combined with quick-search) SHALL only
remove non-matching items from what's displayed — it SHALL NOT change their relative order. The
list's existing "Default Alphabetical Ordering" guarantee (applied whenever the item collection is
replaced) therefore continues to hold for a filtered view exactly as it already does for
quick-search: displayed items are always ascending-alphabetical by identity, never the order a
server happened to return them in — including when an owning tab uses the filter-changed
notification to scope a server-side fetch (see "Filter Changes Are Observable For Optional
Server-Side Scoping"), whose own result order has no bearing on what's ultimately displayed.

#### Scenario: A filtered view stays alphabetically ordered
- **WHEN** the shared filter wiring narrows the list to more than one matching item
- **THEN** those items are displayed in ascending alphabetical order by identity, the same order
  they would appear in unfiltered

#### Scenario: A server-scoped fetch's own order does not leak through
- **WHEN** an owning tab uses the filter-changed notification to scope a server-side fetch, and
  that fetch returns matching items in an order that is not alphabetical
- **THEN** the list still displays them in ascending alphabetical order, exactly as if they had been
  fetched unscoped and narrowed in memory instead
