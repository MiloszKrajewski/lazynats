## MODIFIED Requirements

### Requirement: Read-Only Tab
The OBJ tab's object level SHALL NOT provide any means to create, upload, edit, or delete an
object. The bucket level SHALL NOT provide any means to edit or delete a bucket (creating a
bucket is provided — see "Create Bucket").

#### Scenario: No edit/delete affordance is present at the bucket level
- **WHEN** the OBJ tab is displayed at the bucket level
- **THEN** no keybinding, button, or other control for editing or deleting a bucket is present

#### Scenario: No mutation affordance is present at the object level
- **WHEN** the OBJ tab is displayed at the object level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, uploading, editing, or deleting
  an object is present

## ADDED Requirements

### Requirement: Create Bucket
The system SHALL allow the user to create a new Object Store bucket from the bucket-level list
via Ctrl+N, which opens a modal dialog collecting Name and Max Age. On confirmation the system
SHALL create the bucket on the server with the entered values and refresh the bucket list so the
new bucket is shown and highlighted. Fields not exposed in this dialog (description, max bytes,
storage backend, replica count, placement, metadata, compression, ...) SHALL be created with
safe, explicit defaults rather than left unset, so the resulting bucket is immediately usable —
storage backend in particular always defaults to file-backed storage, since this dialog offers no
way to choose memory-backed storage.

#### Scenario: Ctrl+N opens the create-bucket dialog
- **WHEN** the user presses Ctrl+N while the bucket-level list holds focus
- **THEN** a modal dialog opens with fields for Name and Max Age

#### Scenario: Confirming a valid dialog creates the bucket
- **WHEN** the user fills in a valid Name, optionally sets Max Age, and confirms (Create)
- **THEN** the system creates the bucket on the server, the dialog closes, and the bucket list is
  refreshed with the new bucket shown and highlighted

#### Scenario: Created buckets always use file-backed storage
- **WHEN** a bucket is created via this dialog
- **THEN** the created bucket uses file-backed storage, regardless of any prior bucket's storage
  choice

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-bucket dialog and cancels (Esc) instead of confirming
- **THEN** no bucket is created and the bucket list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the create (e.g. duplicate name,
  invalid bucket name)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-bucket dialog reopens with the previously entered values
  still filled in

#### Scenario: Unset Max Age means unlimited
- **WHEN** a bucket is created via this dialog without setting the Max Age field
- **THEN** the created bucket has no maximum object age

#### Scenario: A set Max Age is applied
- **WHEN** a bucket is created via this dialog with an explicit Max Age value
- **THEN** the created bucket's maximum object age matches the entered value

### Requirement: Create Bucket Field Validation
The create-bucket dialog SHALL validate Name and Max Age before allowing confirmation, and SHALL
visually flag invalid fields rather than allowing a request that will fail immediately.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty Max Age means unlimited
- **WHEN** the Max Age field is left empty and the rest of the dialog is otherwise valid
- **THEN** the Create action is available and the created bucket has no maximum object age

#### Scenario: Unparseable Max Age blocks creation
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Create action is unavailable and the Max Age field is flagged invalid
