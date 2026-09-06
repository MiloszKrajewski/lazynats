## ADDED Requirements

### Requirement: Shared Quick-Search Wiring
The list editor SHALL offer the same shared, opt-in quick-search shape `drillable-list` specifies
(`/` focuses a persistent search field, live in-memory case-insensitive fuzzy-subsequence
filtering of currently-held items, no fetch ever issued, Esc-to-clear-then-fall-through behavior,
a "Search" shortcut hint), applied to the list editor's item collection instead of a drillable
list's. Item order in the filtered view SHALL match the item collection's existing (insertion)
order, since the list editor does not sort.

#### Scenario: Activating quick-search filters the displayed items live
- **WHEN** a subclass activates quick-search and the user types a query into the search field
- **THEN** only items whose text representation fuzzy-matches the query (per the same
  subsequence-matching rule `drillable-list` defines) remain shown, in their existing relative
  order

#### Scenario: A subclass that does not activate quick-search has no search affordance
- **WHEN** a list editor subclass activates none of the shared filtering shapes
- **THEN** `/` has no effect, no search field is present, and no "Search" hint appears among its
  shortcut hints

### Requirement: Shared Filter Wiring
The list editor SHALL offer the same shared, opt-in "Filter" (Ctrl+F) shape `drillable-list`
specifies (modal pattern dialog, seeded with the active pattern, validated and compiled per the
filter-expression grammar, sticky across item-collection changes, combines with quick-search as an
AND, clearable), applied to the list editor's item collection.

#### Scenario: Confirming a valid pattern narrows the displayed items
- **WHEN** a subclass activates the shared filter wiring and the user confirms a valid, non-empty
  pattern
- **THEN** only currently-held items matching that pattern remain shown, in their existing relative
  order

#### Scenario: The filter persists across an item-collection change
- **WHEN** a filter is active and the item collection changes (e.g. a new item is added via Ctrl+N,
  or a subclass rebuilds the collection from an external source of truth)
- **THEN** the filter remains active and continues to narrow the (now-changed) displayed items

### Requirement: Create, Edit, and Delete Act on the Selected Item Regardless of Filtering
When quick-search and/or the shared filter wiring narrow the displayed items, the list's selection
SHALL always refer to an item within the currently-displayed (filtered) subset. Ctrl+E and Ctrl+D
SHALL act on that selected item's actual position within the full underlying item collection, so
editing or deleting a filtered-in item never affects the wrong item; Ctrl+N's newly created item
SHALL be appended to the full underlying item collection exactly as it is when no filter is active,
and SHALL become visible immediately if it matches whatever filter/search is currently active (or
remain hidden if it does not, without this being treated as an error).

#### Scenario: Editing a filtered-in item edits the correct underlying item
- **WHEN** quick-search or the filter narrows the displayed items, the user selects one of the
  displayed items, and presses Ctrl+E
- **THEN** the edit callback receives that same item's current value, not a different item's

#### Scenario: Deleting a filtered-in item removes the correct underlying item
- **WHEN** quick-search or the filter narrows the displayed items, the user selects one of the
  displayed items, and presses Ctrl+D
- **THEN** that same item is removed from the full underlying item collection, and every other item
  (displayed or filtered out) is unaffected

#### Scenario: A newly created item that doesn't match the active filter is not shown
- **WHEN** a filter or quick-search query is active and the user creates a new item via Ctrl+N whose
  text representation does not match it
- **THEN** the new item is appended to the full underlying item collection but does not appear in
  the currently-displayed (filtered) items
