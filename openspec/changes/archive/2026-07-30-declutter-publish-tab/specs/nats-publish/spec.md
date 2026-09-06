## MODIFIED Requirements

### Requirement: Compose Message Fields
The Publish tab SHALL provide an editable Subject field, an editable list of header key/value
pairs managed as whole entries via dedicated New/Edit/Delete actions (not per-character inline
fields), and an editable multi-line text Payload field, all editable independently before
sending.

#### Scenario: Fields are independently editable
- **WHEN** the user enters a subject, adds header pairs via the header entry dialog, and types
  payload text
- **THEN** each field reflects its own entered value without affecting the others

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
