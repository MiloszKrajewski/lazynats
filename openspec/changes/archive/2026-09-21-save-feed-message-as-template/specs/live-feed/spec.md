## ADDED Requirements

### Requirement: Save as Template Shortcut
The system SHALL allow the user to open a Create Template dialog pre-populated from the
highlighted Live Feed message via `T`, while the Live Feed pane is focused and a message is
selected. This is a row-scoped shortcut, using the same selected-message lookup `Enter` already
uses to open the Message Detail dialog, and works identically whether the feed is following
(selection tracks the newest message) or sticky (selection is pinned to a specific message).

#### Scenario: T opens a pre-populated Create Template dialog
- **WHEN** the Live Feed pane is focused, a message is selected, and the user presses `T`
- **THEN** a Create Template dialog opens, pre-populated from that message per `nats-templates`'
  "Save Feed Message as Template" requirement

#### Scenario: T with an empty feed does nothing
- **WHEN** the Live Feed pane is focused and no message is selected (the feed is empty)
- **THEN** pressing `T` opens no dialog

#### Scenario: T works while the feed is following
- **WHEN** the Live Feed pane is focused, the feed is following (selection tracks the newest
  message), and the user presses `T`
- **THEN** the Create Template dialog opens pre-populated from the currently newest message

#### Scenario: T works while the feed is sticky
- **WHEN** the Live Feed pane is focused, the feed is sticky with a specific message selected, and
  the user presses `T`
- **THEN** the Create Template dialog opens pre-populated from that selected message, not the
  newest one

### Requirement: Save as Template Shortcut Is Discoverable
The Live Feed view SHALL advertise its `Save as Template` shortcut (`T`) through the same opt-in
shortcut-source contract `Clear` already uses, so it is included when shortcuts are aggregated for
the currently focused view.

#### Scenario: Save as Template appears in the shortcut picker while the feed is focused
- **WHEN** keyboard focus is on the Live Feed pane and the user opens the shortcut picker (`?`)
- **THEN** the picker lists `Save as Template` among the available shortcuts
