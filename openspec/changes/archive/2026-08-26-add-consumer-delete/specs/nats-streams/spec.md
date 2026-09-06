## MODIFIED Requirements

### Requirement: Read-Only List
The Streams tab's consumer level SHALL NOT provide any means to edit a consumer (deleting a
consumer is provided — see "Delete Consumer"). The stream level SHALL NOT provide any means to
edit a stream (creating and deleting a stream are both provided — see "Create Stream" and "Delete
Stream"; creating a consumer is also provided — see "Create Consumer").

#### Scenario: No edit affordance is present at the stream level
- **WHEN** the Streams tab is displayed at the stream level
- **THEN** no keybinding, button, or other control for editing a stream is present

#### Scenario: No edit affordance is present at the consumer level
- **WHEN** the Streams tab is displayed at the consumer level (drilled into a stream)
- **THEN** no keybinding, button, or other control for editing a consumer is present

## ADDED Requirements

### Requirement: Delete Consumer
The system SHALL allow the user to delete the highlighted consumer from the consumer-level list
via Ctrl+D. Before deleting, the system SHALL prompt the user to confirm, naming the consumer to
be deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the consumer on the server and refresh the
consumer list so the deleted consumer no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the consumer-level list holds focus and a consumer is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted consumer

#### Scenario: Confirming deletes the consumer
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the consumer on the server, and the consumer list is refreshed so
  the deleted consumer no longer appears

#### Scenario: Highlight moves to a neighboring consumer after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the consumer that was immediately below the deleted consumer becomes highlighted, or
  the consumer immediately above it if the deleted consumer was last in the list, or no consumer
  is highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no consumer is deleted and the consumer list is unchanged

#### Scenario: Delete is only reachable at the consumer level
- **WHEN** the Streams tab is displayed at the stream level (not drilled into a stream)
- **THEN** Ctrl+D has no effect on any consumer, since none is displayed
