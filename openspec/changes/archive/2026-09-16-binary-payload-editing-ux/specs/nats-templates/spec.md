## ADDED Requirements

### Requirement: Payload Field Word-Wrap
The create-template/edit-template dialog's Payload field SHALL word-wrap its content when the
selected Payload Type is `Hex`, `Base64`, or `Text`, and SHALL NOT word-wrap when it is `Json`.
Changing the Payload Type field's value while the dialog is open SHALL update the Payload field's
wrap behavior immediately, without requiring the dialog to be closed and reopened.

#### Scenario: Hex payload wraps
- **WHEN** Payload Type is `Hex` and the Payload field's text is wider than the field
- **THEN** the text wraps to additional visible lines instead of requiring horizontal scrolling

#### Scenario: Base64 payload wraps
- **WHEN** Payload Type is `Base64` and the Payload field's text is wider than the field
- **THEN** the text wraps to additional visible lines instead of requiring horizontal scrolling

#### Scenario: Text payload wraps
- **WHEN** Payload Type is `Text` and the Payload field's text is wider than the field
- **THEN** the text wraps to additional visible lines instead of requiring horizontal scrolling

#### Scenario: Json payload does not wrap
- **WHEN** Payload Type is `Json` and the Payload field's text is wider than the field
- **THEN** the text does not wrap; the field scrolls horizontally instead

#### Scenario: Switching Payload Type updates wrap without reopening
- **WHEN** the Payload field already holds text and the user changes Payload Type from `Json` to
  `Hex` (or vice versa)
- **THEN** the Payload field's wrap behavior updates to match the newly selected type immediately

## MODIFIED Requirements

### Requirement: Template Storage
The system SHALL store each template as a JSON document in a dedicated NATS KV bucket named
`lazynats-templates`, one entry per template, keyed by the template's Name. The document SHALL
capture the template's Subject, Headers, Payload Type, and Payload; Name SHALL NOT be duplicated
inside the document since it is already the KV key. When Payload Type is `Json`, the document's
Payload SHALL be stored as a native JSON value (object, array, string, number, boolean, or null),
not as a JSON-encoded string; for every other Payload Type, Payload SHALL be stored as a JSON
string, as today. Reading a document whose Payload was written as a JSON-encoded string under a
`Json` Payload Type (the shape used before this requirement changed) SHALL still succeed, treating
that string as the template's payload text. When Payload Type is `Hex` or `Base64`, the stored
Payload SHALL be the normalized form of the entered text — any whitespace between encoding units
(byte pairs for `Hex`, quanta for `Base64`) discarded, per `payload-types`' Payload Validation
requirement — rather than whatever whitespace the user typed or pasted.

The bucket SHALL be configured with a marker TTL (`LimitMarkerTTL`) of 1 minute, so that
delete/purge markers left behind by Delete Template expire instead of being retained
indefinitely. Any write path that ensures the bucket exists (template create, template edit,
import) SHALL apply this configuration to the bucket, including a bucket that already exists from
before this requirement changed, so the marker TTL takes effect regardless of whether the bucket
was freshly created or already present.

#### Scenario: A saved template is stored as a KV entry
- **WHEN** a template named `get-invoice` with Subject `invoices.get` is created
- **THEN** the `lazynats-templates` bucket contains an entry keyed `get-invoice` whose value is a
  JSON document carrying that template's Subject, Headers, Payload Type, and Payload

#### Scenario: A Json-typed template's payload is stored as a native JSON value
- **WHEN** a template with Payload Type `Json` and Payload text `{"id":1}` is created
- **THEN** the stored document's `payload` field is the native JSON value `{"id":1}`, not the
  JSON string `"{\"id\":1}"`

#### Scenario: A pre-existing string-encoded Json payload still reads correctly
- **WHEN** the `lazynats-templates` bucket contains an entry with Payload Type `Json` whose
  `payload` field is the JSON string `"{\"id\":1}"` (written before this requirement changed)
- **THEN** the system reads that template's Payload as the text `{"id":1}`, the same as it would
  for the native-JSON-value shape

#### Scenario: Creating the first template configures the bucket with a marker TTL
- **WHEN** the `lazynats-templates` bucket does not exist and the user creates the first template
- **THEN** the bucket is created with `LimitMarkerTTL` set to 1 minute

#### Scenario: A pre-existing bucket without a marker TTL is upgraded on the next write
- **WHEN** the `lazynats-templates` bucket already exists with `LimitMarkerTTL` unset (created
  before this requirement changed) and the user creates, edits, or imports a template
- **THEN** the bucket's configuration is updated to set `LimitMarkerTTL` to 1 minute

#### Scenario: A whitespace-formatted Hex payload is stored normalized
- **WHEN** a template with Payload Type `Hex` and Payload text `48 65 6C 6C 6F` (space-separated
  byte pairs) is created
- **THEN** the stored document's `payload` field is `48656C6C6F`, with the whitespace discarded,
  not the space-separated text as entered

#### Scenario: A whitespace-formatted Base64 payload is stored normalized
- **WHEN** a template with Payload Type `Base64` and Payload text spanning multiple lines (e.g.
  pasted from a source that wraps at a fixed width) is created
- **THEN** the stored document's `payload` field is that text with the line-break whitespace
  discarded, as a single unbroken base64 string

### Requirement: Edit Template
The system SHALL allow the user to edit the highlighted template via E, opening the same
modal dialog used for "Create Template" in edit mode: the title and confirm action read "Edit
Template"/"Save", Name is shown but disabled (immutable once the template exists, since it is the
KV key), and Subject, Headers, Payload Type, and Payload are seeded with the template's current
values and remain editable. When Payload Type is `Hex` or `Base64`, the Payload field SHALL be
seeded not with the stored payload text verbatim but with that text rendered for display (the same
rendering `payload-presentation` uses for the read-only Message Detail view), at the dialog's
Payload field width, so the field starts wrapped/grouped for readability rather than as one
unbroken string; for `Json`/`Text`, the Payload field SHALL be seeded with the stored text
unchanged. On confirmation the system SHALL overwrite the entry in the `lazynats-templates` bucket
and refresh the template list so the updated template's row reflects any changed content.

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
  still filled in

#### Scenario: E with no template highlighted does nothing
- **WHEN** the user presses E while the Templates list holds focus and the list is empty (no
  template highlighted)
- **THEN** no edit-template dialog opens

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
