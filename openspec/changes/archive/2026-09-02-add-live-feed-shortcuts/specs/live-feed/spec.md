## ADDED Requirements

### Requirement: Alt+M Focuses the Live Feed
The system SHALL allow the user to move keyboard focus directly into the Live Feed pane from
anywhere in the application via a dedicated `Alt+M` shortcut, mirroring how `Alt+1..5` move focus
into a management tab's content.

#### Scenario: Alt+M focuses the feed from a management tab
- **WHEN** keyboard focus is within a management tab's content and the user presses `Alt+M`
- **THEN** keyboard focus moves into the Live Feed pane

#### Scenario: Alt+M focuses the feed from anywhere focus can reach
- **WHEN** keyboard focus is anywhere in the application that is part of the normal (non-modal)
  key dispatch chain and the user presses `Alt+M`
- **THEN** keyboard focus moves into the Live Feed pane

### Requirement: Clear Shortcut Is Discoverable
The Live Feed view SHALL advertise its `Clear` shortcut (`C`) through the same opt-in
shortcut-source contract every other shortcut-advertising view uses, so it is included when
shortcuts are aggregated for the currently focused view.

#### Scenario: Clear appears in the shortcut picker while the feed is focused
- **WHEN** keyboard focus is on the Live Feed pane and the user opens the shortcut picker (`?`)
- **THEN** the picker lists `Clear` among the available shortcuts

#### Scenario: Clear is not shown as a separate always-visible status-bar widget
- **WHEN** the Live Feed pane is focused
- **THEN** the status bar does not display a dedicated, always-visible `Clear` widget outside of
  the shortcut picker
