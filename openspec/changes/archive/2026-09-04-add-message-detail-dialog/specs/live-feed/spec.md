## ADDED Requirements

### Requirement: Selecting a Message Opens the Message Detail Dialog
The system SHALL open the Message Detail dialog when the user accepts (selects, e.g. via `Enter`)
a message row in the Live Feed, showing that message's full subject, headers, and payload,
replacing the previous subject-only confirmation stub.

#### Scenario: Accepting a feed row opens the detail dialog
- **WHEN** the Live Feed pane is focused, a message row is selected, and the user accepts it
  (e.g. presses `Enter`)
- **THEN** the Message Detail dialog opens for that message
