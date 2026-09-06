## REMOVED Requirements

### Requirement: Read-Only Tab
**Reason**: The object level gains Upload, Download, and Delete (see the ADDED requirements
below). Edit remains unsupported — an Object Store object's content can't be modified in place,
only replaced by uploading over it — but the tab as a whole is no longer read-only.
**Migration**: None. Existing users gain new keybindings (Ctrl+N, Ctrl+S, Ctrl+D at the object
level); nothing about the bucket level or the existing read-only detail panel changes.

## ADDED Requirements

### Requirement: Upload Object
The system SHALL allow the user to upload a local file as a new object in the currently
drilled-into bucket from the object-level list via Ctrl+N, which opens a modal dialog collecting
Key (the object's name) and a local file Path. On confirmation the system SHALL stream the local
file's contents to the server under the given Key and refresh the object list so the new object is
shown and highlighted.

#### Scenario: Ctrl+N opens the upload dialog
- **WHEN** the user presses Ctrl+N while the object-level list holds focus
- **THEN** a modal dialog opens with an editable Key field and an editable Path field, both empty

#### Scenario: Confirming a valid dialog uploads the object
- **WHEN** the user fills in a non-empty Key, a Path pointing to an existing, readable local file,
  and confirms (Upload)
- **THEN** the system streams that file's contents to the currently drilled-into bucket under the
  given Key, the dialog closes, and the object list is refreshed with the new object shown and
  highlighted

#### Scenario: Uploading under an existing Key overwrites it
- **WHEN** the user uploads with a Key that already names an object in the bucket
- **THEN** the system writes the new content under that Key without a separate confirmation
  prompt, matching the Object Store server's own put-always-succeeds semantics

#### Scenario: Cancelling the dialog uploads nothing
- **WHEN** the user opens the upload dialog and cancels (Esc) instead of confirming
- **THEN** no object is uploaded and the object list is unchanged

#### Scenario: Server-side or local-file upload failure is reported without losing entered values
- **WHEN** the user confirms the dialog and either the local file can't be read or the server
  rejects the upload
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the upload dialog reopens with the previously entered Key and Path
  still filled in

#### Scenario: Ctrl+N has no effect at the bucket level
- **WHEN** the user presses Ctrl+N while the bucket-level list holds focus
- **THEN** the upload dialog does not open (Ctrl+N instead opens the create-bucket dialog, per
  "Create Bucket")

### Requirement: Upload Object Field Validation
The upload dialog SHALL validate Key and Path before allowing confirmation, and SHALL visually
flag an invalid field rather than allowing a request that will fail immediately.

#### Scenario: Empty Key blocks upload
- **WHEN** the Key field is empty or whitespace-only
- **THEN** the Upload action is unavailable and the Key field is flagged invalid

#### Scenario: Empty or non-existent Path blocks upload
- **WHEN** the Path field is empty, or names a local path that doesn't exist or isn't a readable
  file
- **THEN** the Upload action is unavailable and the Path field is flagged invalid

### Requirement: Browse For A Local File
The upload and download dialogs SHALL let the user populate the Path field by browsing rather than
typing, via a dedicated in-dialog keybinding that opens a native file-picker view. On a
non-cancelled pick, the chosen path SHALL replace the Path field's current content and validation
SHALL re-run immediately.

#### Scenario: Browsing in the upload dialog opens a file-open picker
- **WHEN** the user triggers Browse while the upload dialog is open
- **THEN** a modal file-picker restricted to selecting an existing file opens

#### Scenario: Browsing in the download dialog opens a file-save picker
- **WHEN** the user triggers Browse while the download dialog is open
- **THEN** a modal file-picker for choosing a destination path (not required to already exist)
  opens

#### Scenario: Picking a file fills in the Path field
- **WHEN** the user selects a path in the file-picker and confirms it
- **THEN** the Path field is set to the selected path and the dialog's field validation re-runs
  against it

#### Scenario: Cancelling the file-picker leaves the Path field unchanged
- **WHEN** the user cancels the file-picker instead of selecting a path
- **THEN** the Path field keeps whatever it held before Browse was triggered

### Requirement: Download Object
The system SHALL allow the user to download the highlighted object's content to a local file from
the object-level list via Ctrl+S, which opens a modal dialog collecting the object's Key (shown but
disabled — the highlighted object's name) and a local file Path. On confirmation the system SHALL
stream the object's content from the server to the given local path.

#### Scenario: Ctrl+S opens the download dialog
- **WHEN** the user presses Ctrl+S while the object-level list holds focus and an object is
  highlighted
- **THEN** a modal dialog opens with the Key field showing the highlighted object's name but
  disabled, and an empty, editable Path field

#### Scenario: Confirming a valid dialog downloads the object
- **WHEN** the user fills in a valid local Path and confirms (Download)
- **THEN** the system streams the highlighted object's content from the server to that local path,
  creating or overwriting the local file, and the dialog closes

#### Scenario: Cancelling the dialog downloads nothing
- **WHEN** the user opens the download dialog and cancels (Esc) instead of confirming
- **THEN** no local file is written and the object list is unchanged

#### Scenario: Server-side or local-file download failure is reported without losing entered values
- **WHEN** the user confirms the dialog and either the server rejects the read or the local path
  can't be written
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the download dialog reopens with the previously entered Path still
  filled in

#### Scenario: Ctrl+S has no effect at the bucket level
- **WHEN** the user presses Ctrl+S while the bucket-level list holds focus
- **THEN** no download dialog opens

#### Scenario: Ctrl+S with no object highlighted does nothing
- **WHEN** the user presses Ctrl+S while the object-level list holds focus and the list is empty
  (no object highlighted)
- **THEN** no download dialog opens

### Requirement: Download Object Field Validation
The download dialog SHALL validate Path before allowing confirmation, and SHALL visually flag an
invalid Path rather than allowing a request that will fail immediately. Key, being disabled, is
exempt from validation.

#### Scenario: Empty Path blocks download
- **WHEN** the Path field is empty or whitespace-only
- **THEN** the Download action is unavailable and the Path field is flagged invalid

### Requirement: Delete Object
The system SHALL allow the user to delete the highlighted object from the object-level list via
Ctrl+D. Before deleting, the system SHALL prompt the user to confirm, naming the object to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the object on the server and refresh the object
list so the deleted object no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the object-level list holds focus and an object is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted object

#### Scenario: Confirming deletes the object
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the object on the server, and the object list is refreshed so the
  deleted object no longer appears

#### Scenario: Highlight moves to a neighboring object after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the object that was immediately below the deleted object becomes highlighted, or the
  object immediately above it if the deleted object was last in the list, or no object is
  highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no object is deleted and the object list is unchanged

#### Scenario: Delete at the object level is independent of bucket-level Delete
- **WHEN** the OBJ tab is displayed at the object level (drilled into a bucket) and the user
  presses Ctrl+D
- **THEN** the highlighted object is deleted per this requirement, not the drilled-into bucket
  (bucket deletion, per "Delete Bucket", is only reachable at the bucket level)

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the object list is left unchanged until the next refresh
