## REMOVED Requirements

### Requirement: Publish Tab
**Reason**: Composing and sending a single message is a one-off action, not something that
benefits from a permanently-reserved management tab. A modal dialog reachable from anywhere via
Alt+P fits the action better and frees the tab strip for the drill-down management views
(Streams/KV/OBJ).
**Migration**: Use the Publish dialog (Alt+P) instead of the "2:Publish" tab. See the "Publish
Dialog" requirement below.

### Requirement: Payload Navigate/Edit Gate
**Reason**: The gate existed solely to keep Tab available for band-to-band focus movement while
Payload used a `TextView`, which otherwise consumes Tab (inserting a literal tab) instead of
letting it bubble up as focus navigation. Setting `TabKeyAddsTab = false` on the `TextView` (the
same mechanism `CreateKeyDialog`'s Value field already relies on) achieves the same outcome
without a Navigate/Edit mode concept.
**Migration**: Payload is now always directly editable; there is no Navigate or Edit mode. Ctrl+E
no longer has any effect on Payload. See the "Payload Is Always Editable" requirement below.

## ADDED Requirements

### Requirement: Publish Dialog
The system SHALL provide a modal Publish dialog for composing and sending a single NATS message,
opened from anywhere in the application via Alt+P, containing Subject, Headers, and Payload
fields and Cancel/Send actions.

#### Scenario: Alt+P opens the Publish dialog
- **WHEN** the user presses Alt+P from anywhere in the application, regardless of which
  management tab is currently selected
- **THEN** the Publish dialog opens, showing empty Subject, Headers, and Payload fields and
  Cancel/Send buttons

#### Scenario: Each open starts empty
- **WHEN** the user closes the Publish dialog (via Cancel or Esc) after entering values, then
  reopens it with Alt+P
- **THEN** the Subject, Headers, and Payload fields are empty, not the values from the previous
  session

#### Scenario: Cancel closes without publishing
- **WHEN** the Publish dialog is open with a valid Subject entered and the user activates Cancel
  (or presses Esc)
- **THEN** the dialog closes and no message is published

### Requirement: Payload Is Always Editable
The Payload field SHALL be directly editable whenever it holds keyboard focus, with no separate
mode to enter first. Tab and Shift-Tab SHALL move keyboard focus to the next/previous band
(Headers/Send) exactly as they do for Subject and the header list, while Up/Down/Left/Right SHALL
move the text cursor within the Payload field per the multi-line text field's normal editing
behavior.

#### Scenario: Typing edits Payload immediately
- **WHEN** Payload has keyboard focus and the user presses a character key
- **THEN** the character is inserted into the Payload field's text at the cursor position, with no
  prior mode switch required

#### Scenario: Tab moves focus out of Payload
- **WHEN** Payload has keyboard focus and the user presses Tab
- **THEN** keyboard focus moves to the Send action, and no tab character is inserted into
  Payload's text

#### Scenario: Up/Down move the text cursor within Payload
- **WHEN** Payload has keyboard focus and its text spans multiple lines
- **THEN** pressing Up or Down moves the text cursor between lines rather than moving keyboard
  focus

## MODIFIED Requirements

### Requirement: Compose Message Fields
The Publish dialog SHALL provide an editable Subject field, an editable list of header key/value
pairs managed as whole entries via dedicated New/Edit/Delete actions (not per-character inline
fields), and a multi-line text Payload field that is directly editable whenever it holds keyboard
focus (see the Payload Is Always Editable requirement), all editable independently before
sending.

#### Scenario: Fields are independently editable
- **WHEN** the user enters a subject, adds header pairs via the header entry dialog, and types
  into Payload
- **THEN** each field reflects its own entered value without affecting the others

### Requirement: Send Feedback and Form Retention
The system SHALL report the outcome of a Send (success or failure) via a status message inline in
the Publish dialog, and SHALL NOT clear the Subject, Headers, or Payload fields after a Send, so
the same message can be edited and resent without closing the dialog.

#### Scenario: Successful send reports status and keeps the form filled
- **WHEN** a message is sent successfully
- **THEN** the Publish dialog shows an inline message reporting the successful publish, and the
  Subject, Headers, and Payload fields remain populated with the values that were just sent

#### Scenario: Failed send reports status and keeps the form filled
- **WHEN** a send attempt fails (e.g. connection error)
- **THEN** the Publish dialog shows an inline message reporting the failure, and the Subject,
  Headers, and Payload fields remain populated as entered, so the user can retry

### Requirement: Framed Field Presentation
The Publish dialog's Subject field, Payload field, and header editor SHALL each be presented
wrapped in a padded `EditFrame`, giving each field visual breathing room without a full bordered
box, and without altering any of their existing editing, validation, or send behavior.

#### Scenario: Subject field is visually framed
- **WHEN** the Publish dialog is displayed
- **THEN** the Subject field is presented inside a padded frame, and its existing validation
  behavior (invalid styling while empty, disabling Send) is unchanged

#### Scenario: Payload field is visually framed
- **WHEN** the Publish dialog is displayed
- **THEN** the Payload field is presented inside a padded frame

#### Scenario: Header editor is visually framed
- **WHEN** the Publish dialog is displayed
- **THEN** the header editor is presented inside a padded frame, and its existing New/Edit/Delete
  behavior is unchanged
