# drillable-list Specification

## Purpose
TBD - created by archiving change extract-drillable-list-base. Update Purpose after archive.
## Requirements
### Requirement: Presenter-Driven List Rendering
A drillable list SHALL delegate formatting an item's value into its list-row text representation
to an injected presenter, and SHALL delegate producing an item's identity (used to preserve
highlight across a refresh) to an overridable accessor, so a new item type can reuse the same
base without the base needing to know its shape.

#### Scenario: Two drillable lists with different item types share the same base
- **WHEN** one drillable list is constructed for one item type and another for a different item
  type, each with its own presenter and identity accessor
- **THEN** both behave identically with respect to list-row rendering, refresh, and empty-state
  handling, differing only in how items are formatted, identified, and in what navigation
  commands each additionally binds

### Requirement: Read-Only Presentation
A drillable list's base SHALL NOT itself provide any means to create, edit, or delete an item on
its own initiative. Any such affordance requires a subclass to explicitly activate the
corresponding shared opt-in wiring (create, edit, or delete) — see "Shared Create Wiring",
"Shared Edit Wiring", and "Shared Delete Wiring". A subclass that activates none of them exposes
no mutation affordance at all.

#### Scenario: No mutation affordance is present by default
- **WHEN** a drillable list subclass activates none of the shared create, edit, or delete wirings
- **THEN** no keybinding, button, or other control for creating, editing, or deleting an item is
  present

### Requirement: Manual Refresh via Ctrl+R
A drillable list SHALL expose a refresh affordance — raising a refresh-requested event that the
owning component (which performs the actual fetch) subscribes to — without itself binding Ctrl+R
as a `KeyBindings` entry. When hosted within a management tab, the owning tab binds Ctrl+R and
invokes this affordance only while this list is the tab's currently active list, per
`tab-scoped-list-shortcuts`. The list SHALL NOT refresh itself on a timer.

#### Scenario: Ctrl+R raises a refresh request
- **WHEN** the user presses Ctrl+R while a drillable list is the currently active list within its
  owning tab
- **THEN** the list's refresh-requested event is raised and the list does not itself alter its
  contents

#### Scenario: The list does not change on its own
- **WHEN** time passes without the user pressing Ctrl+R and without the owning component calling
  `ReplaceItems`
- **THEN** the list's displayed contents are unchanged

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

### Requirement: Empty-State Hint
A drillable list SHALL, while its item collection is empty, display a non-interactive hint line
in place of the (otherwise blank) list content, using text supplied by each subclass. The hint's
visual style SHALL reflect whether the list currently holds keyboard focus, matching the focused/
unfocused distinction used elsewhere in the app's list components.

#### Scenario: Empty collection shows the subclass's hint text
- **WHEN** a drillable list is displayed and its item collection is empty
- **THEN** a hint line supplied by the subclass is shown in place of the list content

#### Scenario: A non-empty collection hides the hint
- **WHEN** the item collection is non-empty
- **THEN** no hint line is shown and the list's items are visible instead

### Requirement: Configurable Background
A drillable list SHALL expose an explicit background color, independent of any implicitly
inherited scheme, applied consistently to both the list's fill and the empty-state hint overlay.

#### Scenario: Setting a background applies it to the list and the hint overlay
- **WHEN** a background color is set on a drillable list
- **THEN** both the list's fill and the empty-state hint (when shown) render using that color

### Requirement: Subclass-Defined Navigation Commands
A drillable list's base SHALL bind no key on its own initiative for the shared shapes forwarded by
an owning tab (Refresh, Create, Delete, Edit, and, where a subclass adds it, a filter operation) —
see `tab-scoped-list-shortcuts`. The base MAY supply shared, opt-in implementations of common
navigation shapes (descend, ascend, create/delete/edit) that a subclass activates explicitly from
its own constructor, each still exposing whether it is enabled and an invocable action for the
owning tab to dispatch to; a subclass MAY also define whatever additional navigation or
level-specific commands it needs beyond those shared shapes, without requiring changes to the base
or to any other subclass. Regardless of whether a shape comes from a shared opt-in implementation
or a subclass's own code, one subclass's activated bindings SHALL have no effect on another
subclass's behavior or on the base's default (no-key-bound) shape.

#### Scenario: One subclass's extra binding does not affect another
- **WHEN** one drillable list subclass binds Enter to a descend action and a different subclass
  binds Esc/Backspace to an ascend action
- **THEN** each subclass responds only to the bindings it defined itself, and neither affects the
  other's behavior or the base class

#### Scenario: Opting into a shared navigation shape does not affect a sibling that didn't opt in
- **WHEN** one drillable list subclass activates a shared opt-in navigation shape (descend,
  ascend, or create/delete) from its constructor, and a different subclass does not activate that
  shape
- **THEN** only the subclass that activated it exposes the corresponding operation to its owning
  tab, and the non-activating subclass's behavior and the base's default shape are unaffected

### Requirement: Neighbor Identity Lookup
A drillable list SHALL expose an operation that, given the identity of an item currently in the
list, returns the identity of the item immediately after it, or — if that item is last — the
identity of the item immediately before it, or `null` if the given identity is not present or the
list contains only that one item.

#### Scenario: Neighbor after a middle item is the next item
- **WHEN** the neighbor identity lookup is called with the identity of an item that has at least
  one item after it in the list
- **THEN** the identity of the next item is returned

#### Scenario: Neighbor after the last item is the previous item
- **WHEN** the neighbor identity lookup is called with the identity of the last item in the list
- **THEN** the identity of the previous item is returned

#### Scenario: Neighbor of the list's only item is null
- **WHEN** the neighbor identity lookup is called with the identity of the list's only item
- **THEN** `null` is returned

#### Scenario: Neighbor of an identity not present in the list is null
- **WHEN** the neighbor identity lookup is called with an identity that does not match any item
  currently in the list
- **THEN** `null` is returned

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

### Requirement: Shared Descend Wiring
A drillable list SHALL offer a shared, opt-in implementation of "descend" navigation: when
activated by a subclass, pressing Enter while the list holds focus SHALL raise a descend-requested
event, without the subclass needing to bind Enter or the event itself.

#### Scenario: Activating descend wiring raises the event on Enter
- **WHEN** a subclass activates the shared descend wiring and the user presses Enter while the
  list holds focus
- **THEN** the descend-requested event is raised

### Requirement: Shared Ascend Wiring
A drillable list SHALL offer a shared, opt-in implementation of "ascend" navigation: when
activated by a subclass, pressing Esc or Backspace while the list holds focus SHALL raise an
ascend-requested event, and an Esc "Back" hint SHALL appear among the list's shortcut hints,
without the subclass needing to bind either key, the event, or the hint itself.

#### Scenario: Activating ascend wiring raises the event on Esc or Backspace
- **WHEN** a subclass activates the shared ascend wiring and the user presses Esc or Backspace
  while the list holds focus
- **THEN** the ascend-requested event is raised

#### Scenario: Activating ascend wiring surfaces a Back hint
- **WHEN** a subclass activates the shared ascend wiring
- **THEN** an Esc "Back" hint is present among the list's shortcut hints

### Requirement: Shared Create Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "create" affordance: when
activated by a subclass, the list exposes a create operation — raising a create-requested event
when invoked, with a "New" label — among the operations its owning tab can dispatch Ctrl+N to per
`tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
delete wiring — a subclass MAY activate create without delete, delete without create, both, or
neither.

#### Scenario: Activating create wiring raises the event on Ctrl+N
- **WHEN** a subclass activates the shared create wiring and the user presses Ctrl+N while this
  list is the active list within its owning tab
- **THEN** the create-requested event is raised

#### Scenario: Activating create wiring surfaces a New operation
- **WHEN** a subclass activates the shared create wiring
- **THEN** a "New" operation is present among the operations this list exposes to its owning tab

#### Scenario: Create wiring without delete wiring has no delete affordance
- **WHEN** a subclass activates the shared create wiring but not the shared delete wiring
- **THEN** Ctrl+D has no effect while this list is the active list, and no "Delete" operation is
  present among the operations this list exposes to its owning tab

### Requirement: Shared Delete Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "delete" affordance: when
activated by a subclass, the list exposes a delete operation — raising a delete-requested event
when invoked, with a "Delete" label — among the operations its owning tab can dispatch Ctrl+D to
per `tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
create wiring — a subclass MAY activate delete without create, create without delete, both, or
neither.

#### Scenario: Activating delete wiring raises the event on Ctrl+D
- **WHEN** a subclass activates the shared delete wiring and the user presses Ctrl+D while this
  list is the active list within its owning tab
- **THEN** the delete-requested event is raised

#### Scenario: Activating delete wiring surfaces a Delete operation
- **WHEN** a subclass activates the shared delete wiring
- **THEN** a "Delete" operation is present among the operations this list exposes to its owning
  tab

#### Scenario: Delete wiring without create wiring has no create affordance
- **WHEN** a subclass activates the shared delete wiring but not the shared create wiring
- **THEN** Ctrl+N has no effect while this list is the active list, and no "New" operation is
  present among the operations this list exposes to its owning tab

#### Scenario: A subclass that activates neither wiring has neither affordance
- **WHEN** a subclass activates neither the shared create wiring nor the shared delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect while this list is the active list, and no "New"/
  "Delete" operations are present among the operations this list exposes to its owning tab

### Requirement: Shared Edit Wiring
A drillable list SHALL offer a shared, opt-in implementation of an "edit" affordance: when
activated by a subclass, the list exposes an edit operation — raising an edit-requested event when
invoked, with an "Edit" label — among the operations its owning tab can dispatch Ctrl+E to per
`tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
create and delete wirings — a subclass MAY activate edit alone, alongside either or both of
create/delete, or none at all.

#### Scenario: Activating edit wiring raises the event on Ctrl+E
- **WHEN** a subclass activates the shared edit wiring and the user presses Ctrl+E while this list
  is the active list within its owning tab
- **THEN** the edit-requested event is raised

#### Scenario: Activating edit wiring surfaces an Edit operation
- **WHEN** a subclass activates the shared edit wiring
- **THEN** an "Edit" operation is present among the operations this list exposes to its owning tab

#### Scenario: Edit wiring is independent of create/delete wiring
- **WHEN** a subclass activates the shared edit wiring but activates neither the shared create nor
  the shared delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect while this list is the active list and no "New"/
  "Delete" operations are exposed, while Ctrl+E and its "Edit" operation behave normally

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
