## REMOVED Requirements

### Requirement: Shared Create/Delete Wiring
**Reason**: Bundling Create and Delete into one opt-in forced a subclass that wanted only one of
the two (the OBJ bucket list, which wanted Create without Delete) to work around the shared
wiring instead of using it. Replaced by two independent requirements, "Shared Create Wiring" and
"Shared Delete Wiring", below.
**Migration**: A subclass previously calling the combined opt-in now calls both `EnableCreate()`
and `EnableDelete()` instead, with identical resulting behavior. A subclass that wants only one of
the two now calls only that one method.

## ADDED Requirements

### Requirement: Shared Create Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "create" affordance: when
activated by a subclass, pressing Ctrl+N while the list holds focus SHALL raise a create-requested
event, and a "New" hint SHALL appear among the list's shortcut hints, without the subclass needing
to bind the key, the event, or the hint itself. This wiring SHALL be activatable independently of
the shared delete wiring — a subclass MAY activate create without delete, delete without create,
both, or neither.

#### Scenario: Activating create wiring raises the event on Ctrl+N
- **WHEN** a subclass activates the shared create wiring and the user presses Ctrl+N while the
  list holds focus
- **THEN** the create-requested event is raised

#### Scenario: Activating create wiring surfaces a New hint
- **WHEN** a subclass activates the shared create wiring
- **THEN** a "New" hint is present among the list's shortcut hints

#### Scenario: Create wiring without delete wiring has no delete affordance
- **WHEN** a subclass activates the shared create wiring but not the shared delete wiring
- **THEN** Ctrl+D has no effect on that list and no "Delete" hint appears among its shortcut hints

### Requirement: Shared Delete Wiring
A drillable list SHALL offer a shared, opt-in implementation of a "delete" affordance: when
activated by a subclass, pressing Ctrl+D while the list holds focus SHALL raise a delete-requested
event, and a "Delete" hint SHALL appear among the list's shortcut hints, without the subclass
needing to bind the key, the event, or the hint itself. This wiring SHALL be activatable
independently of the shared create wiring — a subclass MAY activate delete without create, create
without delete, both, or neither.

#### Scenario: Activating delete wiring raises the event on Ctrl+D
- **WHEN** a subclass activates the shared delete wiring and the user presses Ctrl+D while the
  list holds focus
- **THEN** the delete-requested event is raised

#### Scenario: Activating delete wiring surfaces a Delete hint
- **WHEN** a subclass activates the shared delete wiring
- **THEN** a "Delete" hint is present among the list's shortcut hints

#### Scenario: Delete wiring without create wiring has no create affordance
- **WHEN** a subclass activates the shared delete wiring but not the shared create wiring
- **THEN** Ctrl+N has no effect on that list and no "New" hint appears among its shortcut hints

#### Scenario: A subclass that activates neither wiring has neither affordance
- **WHEN** a subclass activates neither the shared create wiring nor the shared delete wiring
- **THEN** Ctrl+N and Ctrl+D have no effect on that list and no "New"/"Delete" hints appear among
  its shortcut hints
