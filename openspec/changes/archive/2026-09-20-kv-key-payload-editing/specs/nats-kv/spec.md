## MODIFIED Requirements

### Requirement: Create Key
The system SHALL allow the user to create a new key/value entry from the key-level list via
N, which opens a modal dialog collecting Name, Payload Type (per the payload-types capability,
one of `Json`/`Text`/`Base64`/`Hex`, defaulted to `Text`), and Value (a multi-line text field
interpreted according to the selected Payload Type). On confirmation the system SHALL encode
Value to bytes according to the selected Payload Type (per the payload-types capability's Payload
Byte Encoding), write the entry to the currently drilled-into bucket on the server, and refresh the
key list so the new key is shown and highlighted.

#### Scenario: N opens the create-key dialog
- **WHEN** the user presses N while the key-level list holds focus
- **THEN** a modal dialog opens with an editable Name field (empty), a Payload Type selector
  defaulted to `Text`, and a multi-line Value field (empty)

#### Scenario: Confirming a valid dialog creates the key
- **WHEN** the user fills in a non-empty Name, optionally changes Payload Type, optionally enters a
  Value valid for the selected Payload Type (including leaving it empty, or entering multiple lines
  of text), and confirms (Create)
- **THEN** the system encodes Value per the selected Payload Type, writes the entry to the
  currently drilled-into bucket, the dialog closes, and the key list is refreshed with the new key
  shown and highlighted

#### Scenario: A non-Text Payload Type encodes accordingly
- **WHEN** the user selects `Hex` or `Base64` as Payload Type and enters text that is valid for that
  type
- **THEN** the created entry's value is that text's decoded byte sequence, not its UTF-8 text bytes

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-key dialog and cancels (Esc) instead of confirming
- **THEN** no key is created and the key list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the write (e.g. invalid key name)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-key dialog reopens with the previously entered Name,
  Payload Type, and Value still filled in

#### Scenario: N has no effect at the bucket level
- **WHEN** the user presses N while the bucket-level list holds focus
- **THEN** the create-key dialog does not open (N instead opens the create-bucket dialog, per
  "Create Bucket")

### Requirement: Create Key Field Validation
The create-key dialog SHALL validate Name and Value before allowing confirmation, and SHALL
visually flag an invalid field rather than allowing a request that will fail immediately. Name is
valid only if non-empty (after trimming); Value is valid only if it passes Payload Validation (per
the payload-types capability) for the currently selected Payload Type - for `Text`, any value,
including empty, is valid.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty Value is allowed for Text
- **WHEN** Payload Type is `Text`, the Value field is left empty, and Name is valid
- **THEN** the Create action is available and the created key's value is an empty string

#### Scenario: An invalid Value for the selected Payload Type blocks creation
- **WHEN** Payload Type is `Json`, `Base64`, or `Hex` and the Value field's text is not valid for
  that type (per the payload-types capability's Payload Validation)
- **THEN** the Create action is unavailable and the Value field is flagged invalid

#### Scenario: Changing Payload Type re-evaluates Value's validity
- **WHEN** the user changes the Payload Type selector while Value already has text entered
- **THEN** the Create action's availability and the Value field's flagged state immediately reflect
  Value's validity under the newly selected Payload Type

### Requirement: Edit Key
The system SHALL allow the user to edit the highlighted key's value from the key-level list via
E, which opens the same modal dialog used for "Create Key" in edit mode: the title and confirm
action read "Edit Key"/"Save", Name is shown but disabled (immutable once the entry exists), and
Payload Type and Value are seeded from the key's current entry and remain editable. Payload Type
SHALL be seeded from the entry's value classified via the payload-content-probe capability, using
the payload-presentation capability's default type for that classification (`Json`→`Json`,
`Utf8Text`→`Text`, `Binary`→`Hex`). For `Json`/`Hex`/`Base64`, Value SHALL be seeded by rendering
the entry's raw bytes under that seeded Payload Type (per the payload-presentation capability); for
`Text`, Value SHALL be seeded with the entry's raw bytes decoded as plain UTF-8 text, not rendered
through the payload-presentation capability's fixed-width line wrapping (which is sized for a
non-wrapping read-only display and would double-wrap once combined with this dialog's own
word-wrapping Value field). On confirmation the system SHALL encode Value to bytes according to the
selected Payload Type and overwrite the entry on the server, and refresh the key list so the
updated key's detail panel reflects the new value.

#### Scenario: E opens the edit-key dialog
- **WHEN** the user presses E while the key-level list holds focus and a key is highlighted
- **THEN** the system fetches that key's current entry and opens a modal dialog seeded with that
  key's Name, a Payload Type derived from the entry's content classification, and Value rendered
  under that Payload Type, with its title and confirm button reading "Edit"/"Save"

#### Scenario: A JSON-classified value is seeded as Json
- **WHEN** the user presses E on a key whose current value classifies as `Json`
- **THEN** the edit-key dialog opens with Payload Type set to `Json` and Value showing the value
  re-serialized with indentation

#### Scenario: A plain-text-classified value is seeded as Text
- **WHEN** the user presses E on a key whose current value classifies as `Utf8Text`
- **THEN** the edit-key dialog opens with Payload Type set to `Text` and Value showing the decoded
  text

#### Scenario: A binary-classified value is seeded as Hex
- **WHEN** the user presses E on a key whose current value classifies as `Binary`
- **THEN** the edit-key dialog opens with Payload Type set to `Hex` and Value showing the value's
  hexadecimal byte representation, rather than the dialog refusing to open

#### Scenario: Name is locked
- **WHEN** the edit-key dialog is open
- **THEN** the Name field shows the key's current name but cannot be changed

#### Scenario: Confirming updates the value
- **WHEN** the user changes Payload Type and/or Value and confirms (Save)
- **THEN** the system encodes Value per the selected Payload Type, overwrites the entry's value on
  the server, the dialog closes, and the key list is refreshed with the updated key highlighted

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-key dialog and cancels (Esc) instead of confirming
- **THEN** the key is not updated and the key list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the update
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-key dialog reopens with the previously entered Payload Type
  and Value still filled in

#### Scenario: E has no effect at the bucket level
- **WHEN** the user presses E while the bucket-level list holds focus
- **THEN** no edit-key dialog opens (E instead opens the edit-bucket dialog, per "Edit Bucket")

#### Scenario: E with no key highlighted does nothing
- **WHEN** the user presses E while the key-level list holds focus and the list is empty (no
  key highlighted)
- **THEN** no edit-key dialog opens

### Requirement: Create/Edit Key Dialog Sizing
The create-key/edit-key dialog (opened via N or E per "Create Key"/"Edit Key") SHALL size
its Name and Value fields' width, and the Value field's height, responsively to the current
terminal size rather than using a fixed size, up to a maximum cap in each dimension. Below that
cap the dialog SHALL still fit within the terminal. The Payload Type selector added to this dialog
occupies its own fixed-height row between Name and Value and does not itself resize with the
terminal.

#### Scenario: Fields widen on a large terminal
- **WHEN** the create-key or edit-key dialog opens on a terminal wide enough to exceed the
  dialog's preferred width cap plus margin
- **THEN** the Name and Value fields render at the dialog's capped preferred width, wider than
  the dialog's previous fixed 43-column width

#### Scenario: Value field grows taller on a tall terminal
- **WHEN** the create-key or edit-key dialog opens on a terminal tall enough to exceed the Value
  field's minimum height plus the dialog's other fixed rows (including the Payload Type row)
- **THEN** the Value field renders taller than the dialog's previous fixed 10-row height, up to
  its maximum height cap

#### Scenario: Dialog still fits a small terminal
- **WHEN** the create-key or edit-key dialog opens on a terminal at or near the minimum size the
  application already supports
- **THEN** the Value field renders no smaller than its previous fixed 10-row height, and the
  dialog remains fully visible on-screen

#### Scenario: Name field height is unaffected
- **WHEN** the create-key or edit-key dialog opens on any terminal size
- **THEN** the Name field remains a single line of input, unchanged from today

#### Scenario: Payload Type row is a fixed height
- **WHEN** the create-key or edit-key dialog opens on any terminal size
- **THEN** the Payload Type selector renders as a single-line dropdown row, unaffected by the
  terminal's height or width

## REMOVED Requirements

### Requirement: Edit Key Printable-Text Guard
**Reason**: Edit now composes Value through a selectable Payload Type (Json/Text/Base64/Hex) the
same way Publish/Templates do, seeding from the entry's content classification (see "Edit Key")
instead of decoding it as plain UTF-8 text. A value that isn't printable UTF-8 text is no longer
unreachable - it opens seeded as `Hex`, editable and re-savable like any other value.
**Migration**: No action needed. Any key that previously triggered this guard's refusal now opens
normally via E, seeded as `Hex`.
