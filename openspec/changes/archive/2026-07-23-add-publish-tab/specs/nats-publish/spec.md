## ADDED Requirements

### Requirement: Publish Tab
The system SHALL provide a "Publish" tab in the management area's tab view, positioned
immediately after the "Subscriptions" tab, for composing and sending a single NATS message.

#### Scenario: Publish tab is available after Subscriptions
- **WHEN** the user views the management area's tabs
- **THEN** "Subscriptions" is the first tab and "Publish" is the second tab

### Requirement: Compose Message Fields
The Publish tab SHALL provide an editable Subject field, an editable list of header key/value
pairs, and an editable multi-line text Payload field, all editable independently before sending.

#### Scenario: Fields are independently editable
- **WHEN** the user enters a subject, adds header pairs, and types payload text
- **THEN** each field reflects its own entered value without affecting the others

### Requirement: Keyboard-Only Header Entry
The system SHALL allow the user to start a new header entry with Ctrl+N, which clears the header
input row and discards any in-progress edit, and SHALL append the input row's key/value pair as
a new header when the user presses Enter while not editing an existing pair.

#### Scenario: Ctrl+N starts a fresh entry
- **WHEN** the user presses Ctrl+N
- **THEN** the header input row's key and value fields are cleared and any in-progress edit is
  discarded

#### Scenario: Enter appends a new header pair
- **WHEN** the user types a key and a value into the header input row, without having loaded an
  existing pair for editing, and presses Enter
- **THEN** the key/value pair is appended to the headers list and the input row is cleared for
  the next entry

### Requirement: Keyboard-Only Header Edit
The system SHALL allow the user to load a selected header pair back into the input row for
editing with Ctrl+E, and SHALL, on Enter, update that same pair's position in the headers list
with the edited values rather than appending a new pair.

#### Scenario: Ctrl+E loads a header pair for editing
- **WHEN** the user selects an existing header pair in the list and presses Ctrl+E
- **THEN** that pair's key and value are loaded into the header input row for editing

#### Scenario: Enter commits an edited header pair
- **WHEN** the user has loaded a header pair via Ctrl+E, changes its key and/or value, and
  presses Enter
- **THEN** the headers list's entry at that pair's original position is updated to the new
  key/value, and no new pair is appended

### Requirement: Keyboard-Only Header Removal
The system SHALL allow the user to remove a selected header pair from the headers list with
Ctrl+D, without requiring any pointer/mouse action. The Delete key SHALL NOT be bound to header
removal, so it remains available for ordinary text editing within the input fields.

#### Scenario: Ctrl+D removes the selected header pair
- **WHEN** the user selects an existing header pair in the list and presses Ctrl+D
- **THEN** that pair is removed from the headers list and no longer sent with the message

#### Scenario: Delete key edits text, not rows
- **WHEN** the user presses Delete while focus is in the header key or value input field
- **THEN** a character is deleted from that field's text and no header pair is removed from the
  list

### Requirement: Send Validation
The system SHALL disable the Send action whenever the Subject field is empty, and SHALL visually
flag the Subject field as invalid (e.g. distinct color) while it is empty. No other field SHALL
block Send.

#### Scenario: Empty subject disables Send
- **WHEN** the Subject field is empty
- **THEN** the Send action is disabled and the Subject field is shown in its invalid visual state

#### Scenario: Non-empty subject enables Send regardless of other fields
- **WHEN** the Subject field is non-empty, regardless of whether headers or payload are empty
- **THEN** the Send action is enabled

### Requirement: Send Publishes the Message
The system SHALL, when Send is activated, publish a NATS message on the entered subject using the
entered header pairs and the entered payload text (UTF-8 encoded) via the connected NATS
connection.

#### Scenario: Sending publishes with the entered subject, headers, and payload
- **WHEN** the user has entered subject `orders.created`, header pair `tenant`/`acme-co`, payload
  text `{"id":42}`, and activates Send
- **THEN** a NATS message is published on subject `orders.created` carrying the `tenant: acme-co`
  header and the UTF-8 encoded payload `{"id":42}`

### Requirement: Send Feedback and Form Retention
The system SHALL report the outcome of a Send (success or failure) via the status bar, and SHALL
NOT clear the Subject, Headers, or Payload fields after a Send, so the same message can be
edited and resent.

#### Scenario: Successful send reports status and keeps the form filled
- **WHEN** a message is sent successfully
- **THEN** the status bar reports the successful publish and the Subject, Headers, and Payload
  fields remain populated with the values that were just sent

#### Scenario: Failed send reports status and keeps the form filled
- **WHEN** a send attempt fails (e.g. connection error)
- **THEN** the status bar reports the failure and the Subject, Headers, and Payload fields remain
  populated as entered, so the user can retry
