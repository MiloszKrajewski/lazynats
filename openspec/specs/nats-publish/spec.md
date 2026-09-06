# nats-publish

## Purpose

Provide a "Publish" tab in the management area that lets the user compose and send a single
NATS message (subject, headers, payload) via the connected NATS connection, entirely through the
keyboard.

## Requirements

### Requirement: Publish Tab
The system SHALL provide a "Publish" tab in the management area's tab view, positioned
immediately after the "Subscriptions" tab, for composing and sending a single NATS message.

#### Scenario: Publish tab is available after Subscriptions
- **WHEN** the user views the management area's tabs
- **THEN** "Subscriptions" is the first tab and "Publish" is the second tab

### Requirement: Compose Message Fields
The Publish tab SHALL provide an editable Subject field, an editable list of header key/value
pairs managed as whole entries via dedicated New/Edit/Delete actions (not per-character inline
fields), and a multi-line text Payload field whose text can only be changed while the field is in
its Edit mode (see the Payload Navigate/Edit Gate requirement), all editable independently before
sending.

#### Scenario: Fields are independently editable
- **WHEN** the user enters a subject, adds header pairs via the header entry dialog, and enters
  Payload's Edit mode to type payload text
- **THEN** each field reflects its own entered value without affecting the others

### Requirement: Payload Navigate/Edit Gate
The Payload field SHALL default to a Navigate mode whenever it receives keyboard focus, in which
its text is displayed but cannot be changed, Up/Down and Tab/Shift-Tab move keyboard focus to
Subject, Headers, or the Send action, and all other keys are ignored. The system SHALL switch
Payload to an Edit mode when Ctrl+E or Enter is pressed while Payload is focused and in Navigate
mode; while in Edit mode, all keys (including Up/Down/Left/Right/Tab) behave exactly as they did
before this gate existed — moving the text cursor, inserting a literal tab, and editing the text.
The system SHALL switch Payload back to Navigate mode when Esc is pressed while in Edit mode,
without moving keyboard focus away from Payload.

#### Scenario: Tabbing into Payload starts in Navigate mode
- **WHEN** keyboard focus moves onto the Payload field via Tab or Shift-Tab
- **THEN** Payload is in Navigate mode

#### Scenario: Ctrl+E enters Edit mode
- **WHEN** Payload is focused and in Navigate mode and the user presses Ctrl+E
- **THEN** Payload switches to Edit mode

#### Scenario: Enter enters Edit mode
- **WHEN** Payload is focused and in Navigate mode and the user presses Enter
- **THEN** Payload switches to Edit mode

#### Scenario: Esc leaves Edit mode without moving focus
- **WHEN** Payload is focused and in Edit mode and the user presses Esc
- **THEN** Payload switches back to Navigate mode and keyboard focus remains on Payload

#### Scenario: Arrow keys move focus while in Navigate mode
- **WHEN** Payload is focused and in Navigate mode
- **THEN** pressing Up moves keyboard focus to the Headers band and pressing Down moves keyboard
  focus to the Send action

#### Scenario: Typing is ignored while in Navigate mode
- **WHEN** Payload is focused and in Navigate mode and the user presses a character key
- **THEN** the Payload field's text is unchanged

#### Scenario: Typing edits text while in Edit mode
- **WHEN** Payload is focused and in Edit mode and the user presses a character key
- **THEN** the character is inserted into the Payload field's text at the cursor position

### Requirement: Keyboard-Only Header Entry
The system SHALL allow the user to start a new header entry with Ctrl+N while the headers list is
focused, which opens a single-field entry dialog. The system SHALL, when the dialog is committed
with non-empty text, split that text on the first `:` character into a key (the text before the
first `:`, trimmed) and a value (the text after the first `:`, trimmed, or empty if no `:` is
present) and append the resulting pair to the headers list. The system SHALL discard the dialog
and add no header if it is cancelled (Esc).

#### Scenario: Ctrl+N opens the header entry dialog
- **WHEN** the headers list is focused and the user presses Ctrl+N
- **THEN** a single-field dialog for entering a new header opens, seeded empty

#### Scenario: Committing the dialog splits text on the first colon
- **WHEN** the user types `Content-Type: application/json` into the header entry dialog and
  commits it
- **THEN** a header pair with key `Content-Type` and value `application/json` is appended to the
  headers list

#### Scenario: Committing text with no colon uses the whole text as the key
- **WHEN** the user types `no-colon-here` into the header entry dialog (no `:` present) and
  commits it
- **THEN** a header pair with key `no-colon-here` and an empty value is appended to the headers
  list

#### Scenario: Cancelling the dialog adds no header
- **WHEN** the user opens the header entry dialog and presses Esc
- **THEN** no header pair is added to the headers list

### Requirement: Keyboard-Only Header Edit
The system SHALL allow the user to load a selected header pair for editing with Ctrl+E while the
headers list is focused, opening the same single-field dialog seeded with that pair formatted as
`"Key: Value"`. The system SHALL, on commit, split the edited text on the first `:` the same way
as new entries and replace that pair's position in the headers list with the result, rather than
appending a new pair. The system SHALL leave the headers list unchanged if the dialog is
cancelled (Esc).

#### Scenario: Ctrl+E opens the dialog seeded with the selected pair
- **WHEN** the user selects an existing header pair with key `tenant` and value `acme-co` in the
  headers list and presses Ctrl+E
- **THEN** the header entry dialog opens with the text `tenant: acme-co`

#### Scenario: Committing an edit replaces the pair in place
- **WHEN** the user has opened the dialog via Ctrl+E on an existing pair, changes the text, and
  commits it
- **THEN** the headers list's entry at that pair's original position is replaced with the newly
  split key/value, and no new pair is appended

#### Scenario: Cancelling an edit leaves the pair unchanged
- **WHEN** the user opens the dialog via Ctrl+E on an existing pair and presses Esc
- **THEN** the headers list is unchanged

### Requirement: Keyboard-Only Header Removal
The system SHALL allow the user to remove a selected header pair from the headers list with
Ctrl+D while the headers list is focused, directly and without opening the entry dialog or
requiring any pointer/mouse action.

#### Scenario: Ctrl+D removes the selected header pair
- **WHEN** the headers list is focused, the user selects an existing header pair, and presses
  Ctrl+D
- **THEN** that pair is removed from the headers list and no longer sent with the message

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

### Requirement: Framed Field Presentation
The Publish tab's Subject field, Payload field, and header editor SHALL each be presented
wrapped in a padded `EditFrame`, giving each field visual breathing room without a full bordered
box, and without altering any of their existing editing, validation, or send behavior.

#### Scenario: Subject field is visually framed
- **WHEN** the Publish tab is displayed
- **THEN** the Subject field is presented inside a padded frame, and its existing validation
  behavior (invalid styling while empty, disabling Send) is unchanged

#### Scenario: Payload field is visually framed
- **WHEN** the Publish tab is displayed
- **THEN** the Payload field is presented inside a padded frame, unaffected by whether the field
  is currently in Navigate or Edit mode

#### Scenario: Header editor is visually framed
- **WHEN** the Publish tab is displayed
- **THEN** the header editor is presented inside a padded frame, and its existing New/Edit/Delete
  behavior is unchanged
