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
owning component (which performs the actual fetch) subscribes to — without itself binding R as a
`KeyBindings` entry. When hosted within a management tab, the owning tab binds R and invokes this
affordance only while this list is the tab's currently active list, per `tab-scoped-list-shortcuts`.
The list SHALL NOT refresh itself on a timer.

#### Scenario: R raises a refresh request
- **WHEN** the user presses R while a drillable list is the currently active list within its
  owning tab
- **THEN** the list's refresh-requested event is raised and the list does not itself alter its
  contents

#### Scenario: The list does not change on its own
- **WHEN** time passes without the user pressing R and without the owning component calling
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
unfocused distinction used elsewhere in the app's list components. A subclass's hint text SHALL
mention every list-level operation actually available while the list is empty: refresh, always,
plus creation whenever the subclass has creation enabled (`EnableCreate()`).

#### Scenario: Empty collection shows the subclass's hint text
- **WHEN** a drillable list is displayed and its item collection is empty
- **THEN** a hint line supplied by the subclass is shown in place of the list content

#### Scenario: A non-empty collection hides the hint
- **WHEN** the item collection is non-empty
- **THEN** no hint line is shown and the list's items are visible instead

#### Scenario: A creation-enabled list's hint mentions both refresh and creation
- **WHEN** a drillable list has creation enabled and its item collection is empty
- **THEN** the displayed hint text mentions both refreshing (`R`) and adding a new item (`N`)

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
A drillable list SHALL raise a descend-requested event whenever the user presses Enter while the
list holds focus. Enter SHALL always be treated as fully handled by the list itself - regardless
of whether an owning tab subscribes to the descend-requested event - so it never falls through to
move keyboard focus elsewhere.

#### Scenario: Enter raises the descend-requested event
- **WHEN** an owning tab subscribes to the descend-requested event and the user presses Enter
  while the list holds focus
- **THEN** the descend-requested event is raised

#### Scenario: Enter is a no-op on a list with nothing to descend into
- **WHEN** no owning tab subscribes to the descend-requested event (e.g. a "leaf" list, such as a
  KV bucket's key list or the Templates list, that has no further level to drill into) and the
  user presses Enter while the list holds focus
- **THEN** nothing visible happens - in particular, keyboard focus does not move away from the
  list (this previously moved focus to the list's attached search field instead - a pre-existing
  defect in every "leaf" list, noticed and fixed while building `nats-templates`'s own flat
  Templates list)

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
when invoked, with a "New" label — among the operations its owning tab can dispatch N to per
`tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
delete wiring — a subclass MAY activate create without delete, delete without create, both, or
neither.

#### Scenario: Activating create wiring raises the event on N
- **WHEN** a subclass activates the shared create wiring and the user presses N while this
  list is the active list within its owning tab
- **THEN** the create-requested event is raised

#### Scenario: Activating create wiring surfaces a New operation
- **WHEN** a subclass activates the shared create wiring
- **THEN** a "New" operation is present among the operations this list exposes to its owning tab

#### Scenario: Create wiring without delete wiring has no delete affordance
- **WHEN** a subclass activates the shared create wiring but not the shared delete wiring
- **THEN** D has no effect while this list is the active list, and no "Delete" operation is
  present among the operations this list exposes to its owning tab

### Requirement: Shared Delete Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "delete" affordance: when
activated by a subclass, the list exposes a delete operation — raising a delete-requested event
when invoked, with a "Delete" label — among the operations its owning tab can dispatch D to
per `tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
create wiring — a subclass MAY activate delete without create, create without delete, both, or
neither.

#### Scenario: Activating delete wiring raises the event on D
- **WHEN** a subclass activates the shared delete wiring and the user presses D while this list is
  the active list within its owning tab
- **THEN** the delete-requested event is raised

#### Scenario: Activating delete wiring surfaces a Delete operation
- **WHEN** a subclass activates the shared delete wiring
- **THEN** a "Delete" operation is present among the operations this list exposes to its owning
  tab

#### Scenario: Delete wiring without create wiring has no create affordance
- **WHEN** a subclass activates the shared delete wiring but not the shared create wiring
- **THEN** N has no effect while this list is the active list, and no "New" operation is
  present among the operations this list exposes to its owning tab

#### Scenario: A subclass that activates neither wiring has neither affordance
- **WHEN** a subclass activates neither the shared create wiring nor the shared delete wiring
- **THEN** N and D have no effect while this list is the active list, and no "New"/
  "Delete" operations are present among the operations this list exposes to its owning tab

### Requirement: Shared Edit Wiring
A drillable list SHALL offer a shared, opt-in implementation of an "edit" affordance: when
activated by a subclass, the list exposes an edit operation — raising an edit-requested event when
invoked, with an "Edit" label — among the operations its owning tab can dispatch E to per
`tab-scoped-list-shortcuts`, without the subclass needing to bind the key, the event, or the
operation's own advertisement itself. This wiring SHALL be activatable independently of the shared
create and delete wirings — a subclass MAY activate edit alone, alongside either or both of
create/delete, or none at all.

#### Scenario: Activating edit wiring raises the event on E
- **WHEN** a subclass activates the shared edit wiring and the user presses E while this list
  is the active list within its owning tab
- **THEN** the edit-requested event is raised

#### Scenario: Activating edit wiring surfaces an Edit operation
- **WHEN** a subclass activates the shared edit wiring
- **THEN** an "Edit" operation is present among the operations this list exposes to its owning tab

#### Scenario: Edit wiring is independent of create/delete wiring
- **WHEN** a subclass activates the shared edit wiring but activates neither the shared create nor
  the shared delete wiring
- **THEN** N and D have no effect while this list is the active list and no "New"/
  "Delete" operations are exposed, while E and its "Edit" operation behave normally

### Requirement: Shared Quick-Search Wiring
A drillable list SHALL offer a shared, opt-in quick-search shape: when activated by a subclass, `/`
is the *only* way to focus a persistent search field associated with that list — the field's
`CanFocus` is `false` except while active, so no other keyboard path (Tab/Shift+Tab, an arrow key)
or a mouse click reaches it — and a "Search" hint SHALL appear among the list's shortcut hints. On
activation, the field SHALL snapshot its current text. Text entered into the field SHALL filter the
currently-loaded items live, in memory, without issuing any fetch or raising `RefreshRequested`.
Filtering SHALL use case-insensitive fuzzy-subsequence matching against each item's identity: query
text `q` matches an item whose identity contains every character of `q`, in the same relative
order, with any (including zero) characters in between — equivalently, `q` behaves as if a wildcard
were inserted between each of its characters (e.g. `oce` matches as `*o*c*e*`). A filtered result
set SHALL remain in the same ascending alphabetical order as the unfiltered list, since matching is
a pass/fail predicate, not a relevance ranking.

Leaving the field — via Enter, Esc, Tab/Shift+Tab, an arrow key, or a mouse click elsewhere — SHALL
always move keyboard focus to the list and make the field unfocusable again (`CanFocus` reverts to
`false`), through one generic focus-lost mechanism rather than bespoke handling per exit path.
Enter, Tab/Shift+Tab, an arrow key, and a mouse click elsewhere SHALL leave the field's text and the
filtered view exactly as they were at that moment (quick-search is already live, so nothing further
needs to be "applied"). Esc on a non-empty field SHALL instead revert the field's text and the
filtered view to the snapshot taken on activation, discarding whatever was typed during that
activation. Esc on an already-empty field SHALL raise the ascend-requested event on a list that has
also activated the shared ascend wiring; on a list that has not, it SHALL behave the same as Esc on
a non-empty field (revert to the snapshot — which degenerates to remaining empty — then return focus
to the list), rather than leaving focus in the field.

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

#### Scenario: `/` is the only way to focus the search field
- **WHEN** a subclass activates the shared quick-search wiring and the search field is not
  currently active
- **THEN** the field's `CanFocus` is `false`, so Tab/Shift+Tab, an arrow key, and a mouse click
  cannot focus it — only pressing `/` while the list holds focus can

#### Scenario: The field snapshots its text on activation
- **WHEN** the user presses `/` to focus the search field
- **THEN** the field's current text (which may be non-empty, left over from a prior activation) is
  captured as the snapshot for this activation

#### Scenario: Esc on non-empty text reverts to the pre-activation snapshot, not to empty
- **WHEN** the search field holds focus, its text differs from the snapshot taken on activation
  (whether or not it is currently empty), and the user presses Esc
- **THEN** the field's text and the filtered view are restored to that snapshot, and focus returns
  to the list

#### Scenario: Esc on an already-empty search field falls through to ascend
- **WHEN** the search field holds focus, is already empty, and the user presses Esc, on a list that
  has also activated the shared ascend wiring
- **THEN** the same result as pressing Esc directly on the list (ascend) occurs

#### Scenario: Esc on an already-empty search field defocuses when ascend isn't available
- **WHEN** the search field holds focus, is already empty, the user presses Esc, and the list has
  not activated the shared ascend wiring
- **THEN** focus returns to the list and the field becomes unfocusable again, rather than remaining
  focused with nothing having happened

#### Scenario: Enter moves focus from the search field into the list
- **WHEN** the search field holds focus and the user presses Enter
- **THEN** focus moves to the list, the field becomes unfocusable again, and the search field's
  text and the filtered view are left unchanged

#### Scenario: Tab, an arrow key, or a mouse click elsewhere also defocuses the field
- **WHEN** the search field holds focus and the user presses Tab, Shift+Tab, or an arrow key, or
  clicks elsewhere with the mouse
- **THEN** focus moves away from the field, the field becomes unfocusable again, and the search
  field's text and the filtered view are left unchanged

#### Scenario: Search text resets on refresh
- **WHEN** the item collection is replaced (via `R` or any other refresh)
- **THEN** the search field's text is cleared back to empty and the full, newly-replaced item
  collection is shown

#### Scenario: A subclass that does not activate quick-search has no search affordance
- **WHEN** a subclass activates none of the shared shapes including quick-search
- **THEN** `/` has no effect on that list, no search field is present, and no "Search" hint appears
  among its shortcut hints

### Requirement: Shared Filter Wiring
A drillable list SHALL offer a shared, opt-in "Filter" affordance, independent of the shared
quick-search wiring: when activated by a subclass, the list exposes a filter operation — opening a
modal pattern dialog when invoked, with a "Filter" label — among the operations its owning tab can
dispatch F to per `tab-scoped-list-shortcuts`, without the subclass needing to build the
dialog, compile the pattern, or apply it itself. The dialog SHALL be seeded with the currently
active filter pattern, or empty if none is active, and SHALL reject an invalid pattern per the
filter-expression grammar (see `list-filter-affordance`). On a valid, non-empty confirmation the
list SHALL compile the pattern and narrow its currently-loaded items to matches; on an empty
confirmation the active filter SHALL be cleared; cancelling (Esc) SHALL leave the active filter (or
lack of one) unchanged.

#### Scenario: Activating filter wiring surfaces a Filter operation
- **WHEN** a subclass activates the shared filter wiring
- **THEN** a "Filter" operation is present among the operations this list exposes to its owning tab,
  invocable via F while this list is the active list

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
- **THEN** F has no effect on that list, and no "Filter" operation is present among the
  operations it exposes to its owning tab

### Requirement: Active Filter Persists Across a Refresh
Unlike the shared quick-search wiring's search text (which resets on every `ReplaceItems`), an
active filter set via the shared filter wiring SHALL survive `ReplaceItems` unchanged, continuing
to narrow whatever the replaced item collection now holds. The active filter SHALL clear only when
the owning tab explicitly invokes a `ClearFilter` operation the base class exposes for this
purpose — e.g. when the currently-filtered scope itself changes, such as descending into a
different bucket or stream.

#### Scenario: The filter persists across a plain refresh
- **WHEN** a filter is active and the list's item collection is replaced (e.g. via R)
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
