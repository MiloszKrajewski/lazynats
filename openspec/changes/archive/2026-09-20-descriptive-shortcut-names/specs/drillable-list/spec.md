## MODIFIED Requirements

### Requirement: Shared Create Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "create" affordance: when
activated by a subclass with an explicitly supplied label, the list exposes a create operation —
raising a create-requested event when invoked, labeled with exactly that supplied text — among
the operations its owning tab can dispatch N to per `tab-scoped-list-shortcuts`, without the
subclass needing to bind the key, the event, or the operation's own advertisement itself. The
label SHALL be a required argument at activation, with no default the base class supplies on the
subclass's behalf. This wiring SHALL be activatable independently of the shared delete wiring — a
subclass MAY activate create without delete, delete without create, both, or neither.

#### Scenario: Activating create wiring raises the event on N
- **WHEN** a subclass activates the shared create wiring and the user presses N while this
  list is the active list within its owning tab
- **THEN** the create-requested event is raised

#### Scenario: Activating create wiring surfaces the exact supplied label
- **WHEN** a subclass activates the shared create wiring with the label "Add new Stream"
- **THEN** an operation labeled exactly "Add new Stream" is present among the operations this
  list exposes to its owning tab

#### Scenario: Create wiring without delete wiring has no delete affordance
- **WHEN** a subclass activates the shared create wiring but not the shared delete wiring
- **THEN** D has no effect while this list is the active list, and no delete operation is
  present among the operations this list exposes to its owning tab

### Requirement: Shared Delete Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "delete" affordance: when
activated by a subclass with an explicitly supplied label, the list exposes a delete operation —
raising a delete-requested event when invoked, labeled with exactly that supplied text — among
the operations its owning tab can dispatch D to per `tab-scoped-list-shortcuts`, without the
subclass needing to bind the key, the event, or the operation's own advertisement itself. The
label SHALL be a required argument at activation, with no default the base class supplies on the
subclass's behalf. This wiring SHALL be activatable independently of the shared create wiring — a
subclass MAY activate delete without create, create without delete, both, or neither.

#### Scenario: Activating delete wiring raises the event on D
- **WHEN** a subclass activates the shared delete wiring and the user presses D while this list is
  the active list within its owning tab
- **THEN** the delete-requested event is raised

#### Scenario: Activating delete wiring surfaces the exact supplied label
- **WHEN** a subclass activates the shared delete wiring with the label "Delete Stream"
- **THEN** an operation labeled exactly "Delete Stream" is present among the operations this list
  exposes to its owning tab

#### Scenario: Delete wiring without create wiring has no create affordance
- **WHEN** a subclass activates the shared delete wiring but not the shared create wiring
- **THEN** N has no effect while this list is the active list, and no create operation is
  present among the operations this list exposes to its owning tab

#### Scenario: A subclass that activates neither wiring has neither affordance
- **WHEN** a subclass activates neither the shared create wiring nor the shared delete wiring
- **THEN** N and D have no effect while this list is the active list, and neither a create nor a
  delete operation is present among the operations this list exposes to its owning tab

### Requirement: Shared Edit Wiring
A drillable list SHALL offer a shared, opt-in implementation of an "edit" affordance: when
activated by a subclass with an explicitly supplied label, the list exposes an edit operation —
raising an edit-requested event when invoked, labeled with exactly that supplied text — among the
operations its owning tab can dispatch E to per `tab-scoped-list-shortcuts`, without the subclass
needing to bind the key, the event, or the operation's own advertisement itself. The label SHALL
be a required argument at activation, with no default the base class supplies on the subclass's
behalf. This wiring SHALL be activatable independently of the shared create and delete wirings —
a subclass MAY activate edit alone, alongside either or both of create/delete, or none at all.

#### Scenario: Activating edit wiring raises the event on E
- **WHEN** a subclass activates the shared edit wiring and the user presses E while this list
  is the active list within its owning tab
- **THEN** the edit-requested event is raised

#### Scenario: Activating edit wiring surfaces the exact supplied label
- **WHEN** a subclass activates the shared edit wiring with the label "Edit Stream"
- **THEN** an operation labeled exactly "Edit Stream" is present among the operations this list
  exposes to its owning tab

#### Scenario: Edit wiring is independent of create/delete wiring
- **WHEN** a subclass activates the shared edit wiring but activates neither the shared create nor
  the shared delete wiring
- **THEN** N and D have no effect while this list is the active list and neither the create nor the
  delete operation is exposed, while E and its edit operation behave normally

### Requirement: Shared Filter Wiring
A drillable list SHALL offer a shared, opt-in "Filter" affordance, independent of the shared
quick-search wiring: when activated by a subclass with an explicitly supplied label, the list
exposes a filter operation — opening a modal pattern dialog when invoked, labeled with exactly
that supplied text, which SHALL also be used as the modal pattern dialog's own title (the same
string authored once, never two separately-set values that could disagree) — among the operations
its owning tab can dispatch F to per `tab-scoped-list-shortcuts`, without the subclass needing to
build the dialog itself. The label SHALL be a required argument at activation, with no default the
base class supplies on the subclass's behalf. The dialog SHALL be seeded with the currently active
filter pattern, or empty if none is active, and SHALL reject an invalid pattern per the
filter-expression grammar (see `list-filter-affordance`). On a valid, non-empty confirmation the
pattern becomes the list's active filter, narrowing which items are shown; on an empty
confirmation the active filter SHALL be cleared; cancelling (Esc) SHALL leave the active filter (or
lack of one) unchanged.

#### Scenario: Activating filter wiring surfaces the exact supplied label on both the hint and the dialog
- **WHEN** a subclass activates the shared filter wiring with the label "Filter Streams"
- **THEN** an operation labeled exactly "Filter Streams" is present among the operations this list
  exposes to its owning tab, and opening it shows a modal pattern dialog titled exactly "Filter
  Streams"

#### Scenario: Confirming a valid pattern narrows the displayed items
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

#### Scenario: A list that does not activate filter wiring has no Filter operation
- **WHEN** a subclass activates none of the shared shapes including filter wiring
- **THEN** F has no effect on that list, and no filter operation is present among the
  operations this list exposes to its owning tab
