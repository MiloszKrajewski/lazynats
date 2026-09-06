## MODIFIED Requirements

### Requirement: Read-Only List
The Streams tab's consumer level SHALL NOT provide any means to create, edit, or delete a
consumer. The stream level SHALL NOT provide any means to edit or delete a stream (creating a
stream is provided — see "Create Stream").

#### Scenario: No edit/delete affordance is present at the stream level
- **WHEN** the Streams tab is displayed at the stream level
- **THEN** no keybinding, button, or other control for editing or deleting a stream is present

#### Scenario: No mutation affordance is present at the consumer level
- **WHEN** the Streams tab is displayed at the consumer level (drilled into a stream)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a consumer
  is present

## ADDED Requirements

### Requirement: Create Stream
The system SHALL allow the user to create a new JetStream stream from the stream-level list via
Ctrl+N, which opens a modal dialog collecting Name, Subjects, Retention, and Max Age. On
confirmation the system SHALL create the stream on the server with the entered values and
refresh the stream list so the new stream is shown and highlighted. Fields not exposed in this
dialog (storage backend, message/byte/consumer limits, replica count, discard policy, ...) SHALL
be created with safe, explicit defaults rather than left unset, so the resulting stream is
immediately usable.

#### Scenario: Ctrl+N opens the create-stream dialog
- **WHEN** the user presses Ctrl+N while the stream-level list holds focus
- **THEN** a modal dialog opens with fields for Name, Subjects, Retention, and Max Age

#### Scenario: Confirming a valid dialog creates the stream
- **WHEN** the user fills in a valid Name and Subjects, selects a Retention policy, optionally
  sets a Max Age, and confirms (Create)
- **THEN** the system creates the stream on the server, the dialog closes, and the stream list is
  refreshed with the new stream shown and highlighted

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-stream dialog and cancels (Esc) instead of confirming
- **THEN** no stream is created and the stream list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the create (e.g. duplicate name,
  invalid subject)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-stream dialog reopens with the previously entered
  values still filled in

#### Scenario: Unexposed limits default to unlimited, not zero
- **WHEN** a stream is created via this dialog without adjusting any field beyond Name, Subjects,
  Retention, and Max Age
- **THEN** the created stream has no message-count or byte-size limit (accepts messages
  indefinitely up to server-wide limits) and at least one replica, rather than a stream that
  rejects messages immediately

### Requirement: Create Stream Field Validation
The create-stream dialog SHALL validate Name, Subjects, and Max Age before allowing
confirmation, and SHALL visually flag invalid fields rather than allowing a request that will
fail immediately.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty subjects blocks creation
- **WHEN** the Subjects field is empty, or contains only delimiter characters with no actual
  subject text
- **THEN** the Create action is unavailable and the Subjects field is flagged invalid

#### Scenario: Subjects field accepts multiple delimiters
- **WHEN** the user enters subjects separated by any mix of spaces, commas, or semicolons (e.g.
  `orders.*, orders.new;  orders.cancelled`)
- **THEN** the system parses each non-empty, delimiter-separated token as a distinct subject

#### Scenario: Empty Max Age means unlimited
- **WHEN** the Max Age field is left empty and the rest of the dialog is otherwise valid
- **THEN** the Create action is available and the created stream has no maximum message age

#### Scenario: Unparseable Max Age blocks creation
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Create action is unavailable and the Max Age field is flagged invalid

#### Scenario: Retention always has a valid selection
- **WHEN** the create-stream dialog is opened
- **THEN** the Retention field already shows a default selection (Limits) and never blocks
  Create on its own
