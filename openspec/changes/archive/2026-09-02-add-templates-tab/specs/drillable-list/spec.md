## MODIFIED Requirements

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
