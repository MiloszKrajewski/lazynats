## MODIFIED Requirements

### Requirement: Subclass-Defined Navigation Commands
A drillable list's base SHALL bind no key beyond Ctrl+R on its own initiative. The base MAY
supply shared, opt-in implementations of common navigation shapes (descend, ascend, create/
delete) that a subclass activates explicitly from its own constructor; a subclass MAY also define
whatever additional navigation or level-specific commands it needs beyond those shared shapes,
without requiring changes to the base or to any other subclass. Regardless of whether a shape
comes from a shared opt-in implementation or a subclass's own code, one subclass's activated
bindings SHALL have no effect on another subclass's behavior or on the base's default
(Ctrl+R-only) shape.

#### Scenario: One subclass's extra binding does not affect another
- **WHEN** one drillable list subclass binds Enter to a descend action and a different subclass
  binds Esc/Backspace to an ascend action
- **THEN** each subclass responds only to the bindings it defined itself, and neither affects the
  other's behavior or the base class

#### Scenario: Opting into a shared navigation shape does not affect a sibling that didn't opt in
- **WHEN** one drillable list subclass activates a shared opt-in navigation shape (descend,
  ascend, or create/delete) from its constructor, and a different subclass does not activate that
  shape
- **THEN** only the subclass that activated it responds to the corresponding keys, and the
  non-activating subclass's behavior and the base's default shape are unaffected

## ADDED Requirements

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

### Requirement: Shared Create/Delete Wiring
A drillable list SHALL offer a shared, opt-in implementation of "create" and "delete" affordances:
when activated by a subclass, pressing Ctrl+N or Ctrl+D while the list holds focus SHALL raise a
create-requested or delete-requested event respectively, and "New"/"Delete" hints SHALL appear
among the list's shortcut hints, without the subclass needing to bind either key, either event, or
either hint itself.

#### Scenario: Activating create/delete wiring raises events on Ctrl+N and Ctrl+D
- **WHEN** a subclass activates the shared create/delete wiring and the user presses Ctrl+N, then
  separately Ctrl+D, while the list holds focus
- **THEN** the create-requested event is raised on Ctrl+N and the delete-requested event is raised
  on Ctrl+D

#### Scenario: Activating create/delete wiring surfaces New and Delete hints
- **WHEN** a subclass activates the shared create/delete wiring
- **THEN** "New" and "Delete" hints are present among the list's shortcut hints

#### Scenario: A subclass that does not activate create/delete wiring has neither affordance
- **WHEN** a subclass does not activate the shared create/delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect on that list and no "New"/"Delete" hints appear among
  its shortcut hints
