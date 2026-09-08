## MODIFIED Requirements

### Requirement: Alt+0 Focuses the Live Feed
The system SHALL allow the user to move keyboard focus directly into the Live Feed pane from
anywhere in the application via a dedicated `Alt+0` shortcut, mirroring how `Alt+1..5` move focus
into a management tab's content.

#### Scenario: Alt+0 focuses the feed from a management tab
- **WHEN** keyboard focus is within a management tab's content and the user presses `Alt+0`
- **THEN** keyboard focus moves into the Live Feed pane

#### Scenario: Alt+0 focuses the feed from anywhere focus can reach
- **WHEN** keyboard focus is anywhere in the application that is part of the normal (non-modal)
  key dispatch chain and the user presses `Alt+0`
- **THEN** keyboard focus moves into the Live Feed pane

### Requirement: No In-View Header
The live feed view SHALL NOT render its own heading text or divider line; it SHALL rely on its
host container's own title and border for framing, so that the view never duplicates a title
already shown by whatever it is hosted in.

#### Scenario: Feed view renders without a redundant heading
- **WHEN** the live feed view is displayed inside its host frame (titled "0:Live Feed")
- **THEN** the view shows no additional heading text or divider line of its own, and its message
  list starts at the top row of the view's content area
