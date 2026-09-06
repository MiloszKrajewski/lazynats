## MODIFIED Requirements

### Requirement: Read-Only Tab
The OBJ tab's object level SHALL NOT provide any means to create, upload, edit, or delete an
object. Creating, editing, and deleting a bucket are all provided at the bucket level — see
"Create Bucket", "Edit Bucket", and "Delete Bucket".

#### Scenario: No mutation affordance is present at the object level
- **WHEN** the OBJ tab is displayed at the object level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, uploading, editing, or deleting
  an object is present

## ADDED Requirements

### Requirement: Edit Bucket
The system SHALL allow the user to edit the highlighted bucket from the bucket-level list via
Ctrl+E, which opens the same modal dialog used for "Create Bucket" in edit mode: the title and
confirm action read "Edit Bucket"/"Save", Name is shown but disabled (immutable on the server once
the bucket exists), and Max Age remains editable. On confirmation the system SHALL update the
bucket's maximum object age on the server, preserving every other aspect of the bucket's
server-side configuration unchanged, and refresh the bucket list so the updated bucket's detail
panel reflects the new value.

#### Scenario: Ctrl+E opens the edit-bucket dialog
- **WHEN** the user presses Ctrl+E while the bucket-level list holds focus and a bucket is
  highlighted
- **THEN** a modal dialog opens, seeded with that bucket's current Name and Max Age, with its
  title and confirm button reading "Edit"/"Save"

#### Scenario: Name is locked
- **WHEN** the edit-bucket dialog is open
- **THEN** the Name field shows the bucket's current value but cannot be changed

#### Scenario: Confirming updates Max Age without touching other server-side config
- **WHEN** the user changes Max Age and confirms (Save)
- **THEN** the system updates only the bucket's maximum object age on the server, the dialog
  closes, and the bucket list is refreshed with the updated bucket highlighted, its other
  server-side configuration unchanged

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-bucket dialog and cancels (Esc) instead of confirming
- **THEN** the bucket is not updated and the bucket list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the update
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-bucket dialog reopens with the previously entered values
  still filled in

#### Scenario: Ctrl+E has no effect at the object level
- **WHEN** the user presses Ctrl+E while the object-level list holds focus
- **THEN** no edit-bucket dialog opens, since no bucket is displayed at that level

#### Scenario: Ctrl+E with no bucket highlighted does nothing
- **WHEN** the user presses Ctrl+E while the bucket-level list holds focus and the list is empty
  (no bucket highlighted)
- **THEN** no edit-bucket dialog opens

### Requirement: Edit Bucket Field Validation
The edit-bucket dialog SHALL apply the same Max Age validation as "Create Bucket Field Validation"
to the field it leaves editable. Name, being disabled, is exempt from validation.

#### Scenario: Unparseable Max Age blocks saving
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Save action is unavailable and the Max Age field is flagged invalid

#### Scenario: Empty Max Age on save means unlimited
- **WHEN** the Max Age field is cleared and confirmed (Save)
- **THEN** the Save action is available, and saving removes the bucket's maximum object age
