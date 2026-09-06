## MODIFIED Requirements

### Requirement: Read-Only Tab
The KV tab's key level SHALL NOT provide any means to create, edit, or delete a key. The bucket
level SHALL NOT provide any means to edit or delete a bucket (creating a bucket is provided —
see "Create Bucket").

#### Scenario: No edit/delete affordance is present at the bucket level
- **WHEN** the KV tab is displayed at the bucket level
- **THEN** no keybinding, button, or other control for editing or deleting a bucket is present

#### Scenario: No mutation affordance is present at the key level
- **WHEN** the KV tab is displayed at the key level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a key is
  present

## ADDED Requirements

### Requirement: Create Bucket
The system SHALL allow the user to create a new KV bucket from the bucket-level list via Ctrl+N,
which opens a modal dialog collecting Name, Storage backend, History, Max Age, and Limit Marker
TTL. On confirmation the system SHALL create the bucket on the server with the entered values and
refresh the bucket list so the new bucket is shown and highlighted. Fields not exposed in this
dialog (max value size, max bytes, replica count, compression, republish, placement,
mirrors/sources, metadata, ...) SHALL be created with safe, explicit defaults rather than left
unset, so the resulting bucket is immediately usable.

#### Scenario: Ctrl+N opens the create-bucket dialog
- **WHEN** the user presses Ctrl+N while the bucket-level list holds focus
- **THEN** a modal dialog opens with fields for Name, Storage, History, Max Age, and Limit Marker
  TTL, with Storage already defaulted to a valid selection (File)

#### Scenario: Confirming a valid dialog creates the bucket
- **WHEN** the user fills in a valid Name, optionally changes Storage, optionally sets History,
  Max Age, and Limit Marker TTL, and confirms (Create)
- **THEN** the system creates the bucket on the server, the dialog closes, and the bucket list is
  refreshed with the new bucket shown and highlighted

#### Scenario: Memory storage is applied
- **WHEN** the user selects Memory as the Storage backend and confirms (Create)
- **THEN** the created bucket uses memory-backed storage rather than the default file-backed
  storage

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-bucket dialog and cancels (Esc) instead of confirming
- **THEN** no bucket is created and the bucket list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the create (e.g. duplicate name,
  invalid bucket name)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-bucket dialog reopens with the previously entered values
  still filled in

#### Scenario: Unset History defaults to keeping the latest revision
- **WHEN** a bucket is created via this dialog without setting the History field
- **THEN** the created bucket keeps at least the latest revision of each key (a usable bucket),
  rather than a bucket configured to retain zero revisions

#### Scenario: A set History value is applied
- **WHEN** a bucket is created via this dialog with an explicit History value
- **THEN** the created bucket keeps that many historical revisions per key, so a caller can read
  back prior values of a key rather than only its current one

#### Scenario: Unset Max Age means unlimited
- **WHEN** a bucket is created via this dialog without setting the Max Age field
- **THEN** the created bucket has no maximum entry age

#### Scenario: Unset Limit Marker TTL means no delete/purge markers
- **WHEN** a bucket is created via this dialog without setting the Limit Marker TTL field
- **THEN** the created bucket does not retain delete/purge markers for removed keys

#### Scenario: A set Limit Marker TTL is applied
- **WHEN** a bucket is created via this dialog with an explicit Limit Marker TTL value
- **THEN** the created bucket's limit marker TTL matches the entered value, so other clients can
  observe a marker when a key is removed by TTL-based expiry — most relevant when Max Age is left
  unlimited, since per-key TTL is then the bucket's only expiry mechanism

### Requirement: Create Bucket Field Validation
The create-bucket dialog SHALL validate Name, History, Max Age, and Limit Marker TTL before
allowing confirmation, and SHALL visually flag invalid fields rather than allowing a request that
will fail immediately.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Storage always has a valid selection
- **WHEN** the create-bucket dialog is opened
- **THEN** the Storage field already shows a default selection (File) and never blocks Create on
  its own

#### Scenario: Empty History means the default revision count
- **WHEN** the History field is left empty and the rest of the dialog is otherwise valid
- **THEN** the Create action is available and the created bucket keeps the default number of
  revisions per key (at least the latest one)

#### Scenario: Non-positive or unparseable History blocks creation
- **WHEN** the History field contains text that does not parse as a positive integer (including
  zero or a negative number)
- **THEN** the Create action is unavailable and the History field is flagged invalid

#### Scenario: Empty Max Age means unlimited
- **WHEN** the Max Age field is left empty and the rest of the dialog is otherwise valid
- **THEN** the Create action is available and the created bucket has no maximum entry age

#### Scenario: Unparseable Max Age blocks creation
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Create action is unavailable and the Max Age field is flagged invalid

#### Scenario: Empty Limit Marker TTL means markers are disabled
- **WHEN** the Limit Marker TTL field is left empty and the rest of the dialog is otherwise valid
- **THEN** the Create action is available and the created bucket does not retain removal markers

#### Scenario: Unparseable Limit Marker TTL blocks creation
- **WHEN** the Limit Marker TTL field contains text that does not parse as a `TimeSpan`
- **THEN** the Create action is unavailable and the Limit Marker TTL field is flagged invalid
