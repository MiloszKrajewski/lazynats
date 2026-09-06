## ADDED Requirements

### Requirement: Export Templates
The system SHALL allow the user to export every template currently in the `lazynats-templates`
bucket via Ctrl+X while the Templates tab holds focus, writing them as a single JSON object to a
local file the user picks via a native file-save dialog. The object SHALL be keyed by template
name, each value carrying that template's Subject, Headers, Payload Type, and Payload in the same
shape the bucket stores them in (see the modified "Template Storage" requirement).

#### Scenario: Ctrl+X opens a file-save dialog
- **WHEN** the user presses Ctrl+X while the Templates tab holds focus
- **THEN** a native file-save dialog opens for choosing the destination file

#### Scenario: Confirming the dialog writes every template
- **WHEN** the user confirms the file-save dialog with a destination path
- **THEN** the chosen file is written containing every template currently in
  `lazynats-templates`, one JSON object entry per template keyed by name

#### Scenario: Exporting with no templates writes an empty object
- **WHEN** the `lazynats-templates` bucket contains no templates and the user confirms Export
- **THEN** the chosen file is written containing an empty JSON object

#### Scenario: Cancelling the dialog exports nothing
- **WHEN** the user opens the file-save dialog via Ctrl+X and cancels it instead of confirming
- **THEN** no file is written

#### Scenario: A write failure is reported
- **WHEN** the user confirms the file-save dialog and writing the file fails (e.g. the path is not
  writable)
- **THEN** the system shows a modal error dialog whose message is the failure's error text

### Requirement: Import Templates
The system SHALL allow the user to import templates from a local JSON file, in the same shape
Export produces, via Ctrl+O while the Templates tab holds focus, choosing the file via a native
file-open dialog. Every entry in the file SHALL be validated before any write is made: on success,
each entry SHALL be written into the `lazynats-templates` bucket (creating it first if it did not
already exist), with an entry whose name matches an existing template overwriting it; the template
list SHALL then be refreshed. If any entry fails validation, the system SHALL write nothing and
report the failure.

#### Scenario: Ctrl+O opens a file-open dialog
- **WHEN** the user presses Ctrl+O while the Templates tab holds focus
- **THEN** a native file-open dialog opens, restricted to existing files

#### Scenario: Confirming a valid file imports every entry
- **WHEN** the user confirms the file-open dialog with a file containing entries that all pass
  validation
- **THEN** every entry is written into the `lazynats-templates` bucket (creating the bucket first
  if it did not already exist), and the template list is refreshed to include them

#### Scenario: An entry with a colliding name overwrites the existing template
- **WHEN** the imported file contains an entry whose name matches an existing template
- **THEN** the existing template's Subject, Headers, Payload Type, and Payload are overwritten
  with the imported entry's values

#### Scenario: An invalid entry blocks the entire import
- **WHEN** the imported file contains at least one entry that fails validation (e.g. an empty
  Subject, or a Payload that is invalid for its Payload Type)
- **THEN** the system writes nothing to the `lazynats-templates` bucket, and shows a modal error
  dialog identifying the failing entry

#### Scenario: A malformed file blocks the entire import
- **WHEN** the chosen file's contents are not valid JSON, or not shaped as a JSON object of
  template entries
- **THEN** the system writes nothing to the `lazynats-templates` bucket, and shows a modal error
  dialog reporting the failure

#### Scenario: Cancelling the dialog imports nothing
- **WHEN** the user opens the file-open dialog via Ctrl+O and cancels it instead of confirming
- **THEN** no templates are imported and the template list is unchanged

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
that string as the template's payload text.

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

### Requirement: Create Template
The system SHALL allow the user to create a new template via Ctrl+N, opening a modal dialog
collecting Name, Subject, Headers (key/value pairs), Payload Type (`Json`, `Text`, `Base64`, or
`Hex`, defaulting to `Text`), and Payload (multi-line text). On confirmation the system SHALL
ensure the `lazynats-templates` bucket exists (creating it with safe defaults if it does not),
write the new entry, and refresh the template list so the new template is shown and highlighted.

#### Scenario: Ctrl+N opens the create-template dialog
- **WHEN** the user presses Ctrl+N while the Templates list holds focus
- **THEN** a modal dialog opens with empty Name, Subject, Headers, and Payload fields, and Payload
  Type defaulted to `Text`

#### Scenario: The Headers field has no filter/search of its own
- **WHEN** the create-template (or edit-template) dialog is open
- **THEN** its Headers field offers no quick-search ("/") or filter (Ctrl+F) of its own, and is at
  least 3 lines tall regardless of how many headers it currently holds

#### Scenario: Confirming a valid dialog creates the template
- **WHEN** the user fills in a valid Name and Subject, optionally adds Headers, selects a Payload
  Type, enters a Payload valid for that type, and confirms (Create)
- **THEN** the system writes the entry to the `lazynats-templates` bucket (creating the bucket
  first if it did not already exist), the dialog closes, and the template list is refreshed with
  the new template shown and highlighted

#### Scenario: Creating the first template creates the bucket
- **WHEN** the `lazynats-templates` bucket does not exist and the user successfully creates a
  template
- **THEN** the bucket exists afterward, containing that template's entry

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-template dialog and cancels (Esc) instead of confirming
- **THEN** no template is created and the template list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the write to the bucket fails
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-template dialog reopens with the previously entered
  values still filled in

### Requirement: Create Template Field Validation
The create-template dialog SHALL validate Name, Subject, and Payload before allowing confirmation,
and SHALL visually flag an invalid field rather than allowing a request that will fail or produce
an unusable template.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty subject blocks creation
- **WHEN** the Subject field is empty
- **THEN** the Create action is unavailable and the Subject field is flagged invalid

#### Scenario: Invalid JSON blocks creation when Payload Type is Json
- **WHEN** Payload Type is `Json` and the Payload field's text does not parse as valid JSON
- **THEN** the Create action is unavailable and the Payload field is flagged invalid

#### Scenario: Invalid Base64 blocks creation when Payload Type is Base64
- **WHEN** Payload Type is `Base64` and the Payload field's text does not decode as valid base64
- **THEN** the Create action is unavailable and the Payload field is flagged invalid

#### Scenario: Invalid hex blocks creation when Payload Type is Hex
- **WHEN** Payload Type is `Hex` and the Payload field's text does not decode as a valid
  hex-encoded byte sequence
- **THEN** the Create action is unavailable and the Payload field is flagged invalid

#### Scenario: Any text is valid when Payload Type is Text
- **WHEN** Payload Type is `Text`
- **THEN** the Payload field's text is never flagged invalid, including when it is empty

#### Scenario: Switching Payload Type re-validates the current Payload text
- **WHEN** the Payload field already contains text that is invalid for the newly selected Payload
  Type (e.g. non-JSON text left over after switching from `Text` to `Json`)
- **THEN** the Create action becomes unavailable and the Payload field is flagged invalid without
  requiring the user to re-type the Payload text
