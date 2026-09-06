## MODIFIED Requirements

### Requirement: Read-Only List
The Streams tab's consumer level SHALL NOT provide any means to create, edit, or delete a
consumer. The stream level SHALL NOT provide any means to edit a stream (creating and deleting a
stream are both provided — see "Create Stream" and "Delete Stream").

#### Scenario: No edit affordance is present at the stream level
- **WHEN** the Streams tab is displayed at the stream level
- **THEN** no keybinding, button, or other control for editing a stream is present

#### Scenario: No mutation affordance is present at the consumer level
- **WHEN** the Streams tab is displayed at the consumer level (drilled into a stream)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a consumer
  is present

## ADDED Requirements

### Requirement: Delete Stream
The system SHALL allow the user to delete the highlighted stream from the stream-level list via
Ctrl+D. Before deleting, the system SHALL prompt the user to confirm, naming the stream to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the stream on the server and refresh the stream
list so the deleted stream no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the stream-level list holds focus and a stream is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted stream

#### Scenario: Confirming deletes the stream
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the stream on the server, and the stream list is refreshed so the
  deleted stream no longer appears

#### Scenario: Highlight moves to a neighboring stream after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the stream that was immediately below the deleted stream becomes highlighted, or the
  stream immediately above it if the deleted stream was last in the list, or no stream is
  highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no stream is deleted and the stream list is unchanged

#### Scenario: Enter alone does not delete
- **WHEN** the deletion prompt is open and the user presses Enter without first moving focus to
  the Delete option
- **THEN** the prompt's default (Cancel) response is activated and no stream is deleted

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms the deletion prompt and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the stream list is unchanged

#### Scenario: Ctrl+D with no stream highlighted does nothing
- **WHEN** the user presses Ctrl+D while the stream-level list holds focus and the list is empty
  (no stream highlighted)
- **THEN** no confirmation prompt opens and nothing is deleted
