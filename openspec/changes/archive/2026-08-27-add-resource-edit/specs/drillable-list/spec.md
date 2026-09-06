## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Shared Edit Wiring
A drillable list SHALL offer a shared, opt-in implementation of an "edit" affordance: when
activated by a subclass, pressing Ctrl+E while the list holds focus SHALL raise an edit-requested
event, and an "Edit" hint SHALL appear among the list's shortcut hints, without the subclass
needing to bind the key, the event, or the hint itself. This wiring SHALL be activatable
independently of the shared create and delete wirings — a subclass MAY activate edit alone,
alongside either or both of create/delete, or none at all.

#### Scenario: Activating edit wiring raises the event on Ctrl+E
- **WHEN** a subclass activates the shared edit wiring and the user presses Ctrl+E while the list
  holds focus
- **THEN** the edit-requested event is raised

#### Scenario: Activating edit wiring surfaces an Edit hint
- **WHEN** a subclass activates the shared edit wiring
- **THEN** an "Edit" hint is present among the list's shortcut hints

#### Scenario: Edit wiring is independent of create/delete wiring
- **WHEN** a subclass activates the shared edit wiring but activates neither the shared create nor
  the shared delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect on that list and no "New"/"Delete" hints appear, while
  Ctrl+E and its "Edit" hint behave normally
