## MODIFIED Requirements

### Requirement: Read-Only Tab
The KV tab's key level SHALL NOT provide any means to create, edit, or delete a key. Creating,
editing, and deleting a bucket are all provided at the bucket level — see "Create Bucket", "Edit
Bucket", and "Delete Bucket".

#### Scenario: No mutation affordance is present at the key level
- **WHEN** the KV tab is displayed at the key level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a key is
  present

## ADDED Requirements

### Requirement: Edit Bucket
The system SHALL allow the user to edit the highlighted bucket from the bucket-level list via
Ctrl+E, which opens the same modal dialog used for "Create Bucket" in edit mode: the title and
confirm action read "Edit Bucket"/"Save", Name and Storage are shown but disabled (both are
immutable on the server once the bucket exists), and History, Max Age, and Limit Marker TTL remain
editable. On confirmation the system SHALL update the bucket on the server with the edited values,
preserving every field the dialog does not expose (max value size, max bytes, replica count,
compression, republish, placement, mirrors/sources, metadata, ...) unchanged from the bucket's
current server-side configuration, and refresh the bucket list so the updated bucket's detail
panel reflects the new values.

#### Scenario: Ctrl+E opens the edit-bucket dialog
- **WHEN** the user presses Ctrl+E while the bucket-level list holds focus and a bucket is
  highlighted
- **THEN** a modal dialog opens, seeded with that bucket's current Name, Storage, History, Max
  Age, and Limit Marker TTL, with its title and confirm button reading "Edit"/"Save"

#### Scenario: Name and Storage are locked
- **WHEN** the edit-bucket dialog is open
- **THEN** the Name and Storage fields show the bucket's current values but cannot be changed

#### Scenario: Confirming updates History, Max Age, and Limit Marker TTL without touching other server-side config
- **WHEN** the user changes any of History, Max Age, or Limit Marker TTL and confirms (Save)
- **THEN** the system updates only those fields on the server, the dialog closes, and the bucket
  list is refreshed with the updated bucket highlighted, its other server-side configuration
  unchanged

#### Scenario: Limit Marker TTL can be enabled on a bucket that previously had none
- **WHEN** the highlighted bucket was created without a Limit Marker TTL and the user sets one
  during edit, then confirms (Save)
- **THEN** the update succeeds and the bucket subsequently retains delete/purge markers per the
  entered TTL

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-bucket dialog and cancels (Esc) instead of confirming
- **THEN** the bucket is not updated and the bucket list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the update
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-bucket dialog reopens with the previously entered values
  still filled in

#### Scenario: Ctrl+E has no effect at the key level
- **WHEN** the user presses Ctrl+E while the key-level list holds focus
- **THEN** no edit-bucket dialog opens, since no bucket is displayed at that level

#### Scenario: Ctrl+E with no bucket highlighted does nothing
- **WHEN** the user presses Ctrl+E while the bucket-level list holds focus and the list is empty
  (no bucket highlighted)
- **THEN** no edit-bucket dialog opens

### Requirement: Edit Bucket Field Validation
The edit-bucket dialog SHALL apply the same History, Max Age, and Limit Marker TTL validation as
"Create Bucket Field Validation" to the fields it leaves editable. Name and Storage, being
disabled, are exempt from validation.

#### Scenario: Non-positive or unparseable History blocks saving
- **WHEN** the History field contains text that does not parse as a positive integer (including
  zero or a negative number)
- **THEN** the Save action is unavailable and the History field is flagged invalid

#### Scenario: Unparseable Max Age blocks saving
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Save action is unavailable and the Max Age field is flagged invalid

#### Scenario: Unparseable Limit Marker TTL blocks saving
- **WHEN** the Limit Marker TTL field contains text that does not parse as a `TimeSpan`
- **THEN** the Save action is unavailable and the Limit Marker TTL field is flagged invalid

#### Scenario: Empty History, Max Age, or Limit Marker TTL on save uses the same meaning as Create
- **WHEN** History, Max Age, or Limit Marker TTL is cleared to empty and the rest of the dialog is
  otherwise valid
- **THEN** the Save action is available, and saving applies that field's "unset" meaning from
  "Create Bucket" (default revision count, unlimited age, or markers disabled, respectively)
