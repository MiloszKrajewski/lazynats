# nats-publish

## Purpose

Provide a modal Publish dialog, reachable from anywhere via Alt+P, that lets the user compose and
send a single NATS message (subject, headers, payload) via the connected NATS connection, entirely
through the keyboard.

## Requirements

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

### Requirement: Compose Message Fields
The Publish dialog SHALL provide an editable Subject field, an editable list of header key/value
pairs managed as whole entries via dedicated New/Edit/Delete actions (not per-character inline
fields), a Payload Type field (`Json`, `Text`, `Base64`, or `Hex`, defaulting to `Text`, per
`payload-types`), and a multi-line text Payload field that is directly editable whenever it holds
keyboard focus (see the Payload Is Always Editable requirement), all editable independently before
sending.

#### Scenario: Fields are independently editable
- **WHEN** the user enters a subject, adds header pairs via the header entry dialog, selects a
  Payload Type, and types into Payload
- **THEN** each field reflects its own entered value without affecting the others

#### Scenario: Payload Type defaults to Text
- **WHEN** the Publish dialog opens
- **THEN** the Payload Type field is set to `Text`

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

### Requirement: Header List Quick-Search
The system SHALL allow the user to narrow the headers list live, in memory, via the same shared
quick-search shape (`/`, case-insensitive fuzzy-subsequence matching against each header pair's
`"Key: Value"` text representation) that other lists in the app offer, with no effect on which
headers are actually sent with the message.

#### Scenario: Typing a query narrows the displayed headers
- **WHEN** the user presses `/` and types a query matching some, but not all, header pairs' `"Key:
  Value"` text as a case-insensitive subsequence
- **THEN** only the matching header pairs remain shown, and every header (shown or not) is still
  sent with the message

#### Scenario: Clearing the query restores the full list
- **WHEN** the search field's text is cleared to empty
- **THEN** every header pair is shown again

### Requirement: Header List Filter
The system SHALL allow the user to set a filter pattern via Ctrl+F that narrows the headers list to
pairs whose `"Key: Value"` text representation matches it, using the same `* ? >`
filter-expression grammar as every other list's Ctrl+F filter (see `list-filter-affordance`). Since
the headers list has no server-side fetch to scope, this filter narrows only what is displayed,
with no effect on which headers are sent. This is independent of quick-search (`/`); both may be
active at once.

#### Scenario: Confirming a valid pattern narrows the header list
- **WHEN** the user presses Ctrl+F, enters a valid, non-empty pattern, and confirms
- **THEN** only header pairs whose `"Key: Value"` text matches the filter expression remain shown,
  and every header (shown or not) is still sent with the message

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared and every header pair is shown again

#### Scenario: Adding a header while filtered may leave it hidden
- **WHEN** a filter is active and the user adds a new header pair whose `"Key: Value"` text does
  not match it
- **THEN** the new header pair is added and will still be sent with the message, but does not
  appear in the currently-filtered list

### Requirement: Send Validation
The system SHALL disable the Send action whenever the Subject field is empty, or whenever the
Payload field's text is invalid for the currently selected Payload Type (per `payload-types`'
Payload Validation requirement), and SHALL visually flag whichever of those fields is currently
invalid (e.g. distinct color). No other field SHALL block Send.

#### Scenario: Empty subject disables Send
- **WHEN** the Subject field is empty
- **THEN** the Send action is disabled and the Subject field is shown in its invalid visual state

#### Scenario: Payload invalid for the selected Payload Type disables Send
- **WHEN** the Subject field is non-empty and the Payload field's text is invalid for the
  currently selected Payload Type (e.g. Payload Type is `Base64` and the text does not decode as
  base64)
- **THEN** the Send action is disabled and the Payload field is shown in its invalid visual state

#### Scenario: Non-empty subject and valid payload enable Send regardless of other fields
- **WHEN** the Subject field is non-empty and the Payload field's text is valid for the currently
  selected Payload Type, regardless of whether headers are empty
- **THEN** the Send action is enabled

### Requirement: Send Publishes the Message
The system SHALL, when Send is activated, publish a NATS message on the entered subject using the
entered header pairs and the Payload field's text encoded to bytes according to the currently
selected Payload Type (per `payload-types`' Payload Byte Encoding requirement), via the connected
NATS connection.

#### Scenario: Sending publishes with the entered subject, headers, and payload
- **WHEN** the user has entered subject `orders.created`, header pair `tenant`/`acme-co`, Payload
  Type `Text`, payload text `{"id":42}`, and activates Send
- **THEN** a NATS message is published on subject `orders.created` carrying the `tenant: acme-co`
  header and the UTF-8 encoded payload `{"id":42}`

#### Scenario: Sending with Payload Type Base64 publishes the decoded bytes
- **WHEN** the user has entered a valid subject, selected Payload Type `Base64`, entered a valid
  base64 payload string, and activates Send
- **THEN** a NATS message is published carrying that string's decoded byte sequence as its
  payload, not its UTF-8 text bytes

#### Scenario: Sending with Payload Type Hex publishes the decoded bytes
- **WHEN** the user has entered a valid subject, selected Payload Type `Hex`, entered a valid
  hex-encoded payload string, and activates Send
- **THEN** a NATS message is published carrying that string's hex-decoded byte sequence as its
  payload, not its UTF-8 text bytes

### Requirement: Send Feedback and Close-on-Success
The system SHALL close the Publish dialog automatically when a Send succeeds. When a Send fails,
the system SHALL report the failure via a status message inline in the Publish dialog and SHALL
NOT clear or close the dialog, so the message can be corrected and resent.

#### Scenario: Successful send closes the dialog
- **WHEN** a message is sent successfully
- **THEN** the Publish dialog closes, the same as activating Cancel

#### Scenario: Failed send reports status and keeps the form filled
- **WHEN** a send attempt fails (e.g. connection error)
- **THEN** the Publish dialog remains open, shows an inline message reporting the failure, and the
  Subject, Headers, and Payload fields remain populated as entered, so the user can retry

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
