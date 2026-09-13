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

### Requirement: Delete Template
The system SHALL allow the user to delete the highlighted template via D. Before deleting,
the system SHALL prompt the user to confirm, naming the template to be deleted, with the
non-destructive choice (Cancel) as the prompt's default (Enter-activated) response. On
confirmation the system SHALL purge the entry from the `lazynats-templates` bucket — removing
prior revisions immediately and leaving a purge marker that expires after the bucket's marker TTL,
rather than a tombstone retained indefinitely — and refresh the template list so the deleted
template no longer appears. If the bucket does not yet carry a marker TTL (e.g. it was created
before this requirement changed and no write/import has upgraded it yet), the system SHALL apply
the bucket's configured marker TTL before purging.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the Templates list holds focus and a template is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted template

#### Scenario: Confirming deletes the template
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system purges the entry from the `lazynats-templates` bucket, and the template
  list is refreshed so the deleted template no longer appears

#### Scenario: Highlight moves to a neighboring template after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the template that was immediately below the deleted template becomes highlighted, or
  the template immediately above it if the deleted template was last in the list, or no template
  is highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no template is deleted and the template list is unchanged

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the delete fails
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the template list is left unchanged until the next refresh
