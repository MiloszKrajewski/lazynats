## MODIFIED Requirements

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

## ADDED Requirements

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
