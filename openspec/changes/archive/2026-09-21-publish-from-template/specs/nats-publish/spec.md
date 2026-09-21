## MODIFIED Requirements

### Requirement: Publish Dialog
The system SHALL provide a modal Publish dialog for composing and sending a single NATS message,
containing Subject, Headers, and Payload fields and Cancel/Send actions. It SHALL be openable
either empty, from anywhere in the application via Alt+P, or pre-populated with an existing
message's Subject, Headers, Payload Type, and Payload (e.g. via a template's Publish shortcut -
see `nats-templates`), with no difference in the dialog's Cancel/Send/close-on-success behavior
between the two.

#### Scenario: Alt+P opens the Publish dialog
- **WHEN** the user presses Alt+P from anywhere in the application, regardless of which
  management tab is currently selected
- **THEN** the Publish dialog opens, showing empty Subject, Headers, and Payload fields and
  Cancel/Send buttons

#### Scenario: Each open via Alt+P starts empty
- **WHEN** the user closes the Publish dialog (via Cancel or Esc) after entering values, then
  reopens it with Alt+P
- **THEN** the Subject, Headers, and Payload fields are empty, not the values from the previous
  session

#### Scenario: Cancel closes without publishing
- **WHEN** the Publish dialog is open with a valid Subject entered and the user activates Cancel
  (or presses Esc)
- **THEN** the dialog closes and no message is published

#### Scenario: Opening pre-populated seeds Subject, Headers, Payload Type, and Payload
- **WHEN** the Publish dialog is opened pre-populated with an existing message
- **THEN** the Subject field shows that message's subject, the Headers list shows its header
  pairs, the Payload Type field shows its payload type, and the Payload field shows its payload
  (rendered per the "Pre-Populated Payload Rendering" requirement) instead of opening empty

#### Scenario: Pre-populated fields remain independently editable before sending
- **WHEN** the Publish dialog was opened pre-populated
- **THEN** the Subject, Headers, Payload Type, and Payload fields can each be changed before
  Send, exactly as when the dialog was opened empty

## ADDED Requirements

### Requirement: Pre-Populated Payload Rendering
When the Publish dialog is opened pre-populated (see "Publish Dialog"), the system SHALL seed the
Payload field by rendering the message's payload bytes for display, per the
`payload-edit-section` capability's seeding-from-bytes rule: `Json`, `Hex`, and `Base64` SHALL be
rendered for display (the same rendering `payload-presentation` uses for the read-only Message
Detail view, and the same rendering Templates' Edit Template dialog already seeds its own Payload
field with), at the dialog's Payload field width, so the field starts formatted for readability
(pretty-printed for `Json`, wrapped/grouped for `Hex`/`Base64`) rather than as whatever was last
stored; `Text` SHALL be seeded by decoding the payload's bytes as plain UTF-8 text, not rendered
through that fixed-width line wrapping.

#### Scenario: A pre-populated Json payload renders pretty-printed
- **WHEN** the Publish dialog is opened pre-populated with a Payload Type `Json` message whose
  stored payload is minified (no insignificant whitespace)
- **THEN** the Payload field shows that payload re-serialized with indentation, rather than the
  minified stored string

#### Scenario: A pre-populated Hex payload renders grouped
- **WHEN** the Publish dialog is opened pre-populated with a Payload Type `Hex` message whose
  stored payload is the normalized (unbroken) text `48656C6C6F`
- **THEN** the Payload field shows that payload rendered as grouped byte pairs, rather than the
  unbroken stored string

#### Scenario: A pre-populated Base64 payload renders wrapped
- **WHEN** the Publish dialog is opened pre-populated with a Payload Type `Base64` message whose
  stored payload is a long unbroken base64 string
- **THEN** the Payload field shows that payload wrapped into multiple lines, rather than the
  unbroken stored string

#### Scenario: A pre-populated Text payload renders as plain decoded text
- **WHEN** the Publish dialog is opened pre-populated with a Payload Type `Text` message
- **THEN** the Payload field shows that payload's bytes decoded as plain UTF-8 text, not passed
  through Json/Hex/Base64-style fixed-width rendering
