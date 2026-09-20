## MODIFIED Requirements

### Requirement: Edit Template
The system SHALL allow the user to edit the highlighted template via E, opening the same
modal dialog used for "Create Template" in edit mode: the title and confirm action read "Edit
Template"/"Save", Name is shown but disabled (immutable once the template exists, since it is the
KV key), and Subject, Headers, Payload Type, and Payload are seeded with the template's current
values and remain editable. The Payload field SHALL be seeded by rendering the stored payload as
bytes for display, per the `payload-edit-section` capability's seeding-from-bytes rule: `Json`,
`Hex`, and `Base64` SHALL be rendered for display (the same rendering `payload-presentation` uses
for the read-only Message Detail view), at the dialog's Payload field width, so the field starts
formatted for readability (pretty-printed for `Json`, wrapped/grouped for `Hex`/`Base64`) rather
than as whatever was last stored; `Text` SHALL be seeded by decoding the stored payload's bytes as
plain UTF-8 text, not rendered through that fixed-width line wrapping. On confirmation the system
SHALL overwrite the entry in the `lazynats-templates` bucket and refresh the template list so the
updated template's row reflects any changed content.

#### Scenario: E opens the edit-template dialog
- **WHEN** the user presses E while the Templates list holds focus and a template is
  highlighted
- **THEN** a modal dialog opens, seeded with that template's current Name, Subject, Headers,
  Payload Type, and Payload, with its title and confirm button reading "Edit"/"Save"

#### Scenario: Name is locked
- **WHEN** the edit-template dialog is open
- **THEN** the Name field shows the template's current name but cannot be changed

#### Scenario: Confirming updates the template
- **WHEN** the user changes any of Subject, Headers, Payload Type, or Payload and confirms (Save)
- **THEN** the system overwrites the entry in the `lazynats-templates` bucket, the dialog closes,
  and the template list is refreshed with the updated template highlighted

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-template dialog and cancels (Esc) instead of confirming
- **THEN** the template is not updated and the template list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the write to the bucket fails
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-template dialog reopens with the previously entered values
  still filled in verbatim (per `payload-edit-section`'s seeding-from-already-typed-text rule, not
  re-rendered from bytes)

#### Scenario: E with no template highlighted does nothing
- **WHEN** the user presses E while the Templates list holds focus and the list is empty (no
  template highlighted)
- **THEN** no edit-template dialog opens

#### Scenario: Opening a Json template for editing shows it pretty-printed
- **WHEN** the user presses E on a template with Payload Type `Json` whose stored Payload is
  minified (no insignificant whitespace)
- **THEN** the edit-template dialog's Payload field shows that payload re-serialized with
  indentation (matching the Message Detail view's Json rendering) rather than the minified stored
  string

#### Scenario: Opening a Hex template for editing shows it formatted for readability
- **WHEN** the user presses E on a template with Payload Type `Hex` whose stored Payload is the
  normalized text `48656C6C6F`
- **THEN** the edit-template dialog's Payload field shows that payload rendered as grouped byte
  pairs (matching the Message Detail view's Hex rendering) rather than the unbroken stored string

#### Scenario: Opening a Base64 template for editing shows it formatted for readability
- **WHEN** the user presses E on a template with Payload Type `Base64` whose stored Payload is a
  long unbroken base64 string
- **THEN** the edit-template dialog's Payload field shows that payload wrapped into multiple lines
  (matching the Message Detail view's Base64 rendering) rather than the unbroken stored string
