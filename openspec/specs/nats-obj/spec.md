# nats-obj Specification

## Purpose
TBD - created by syncing change add-obj-store-tab. Update Purpose after archive.

## Requirements
### Requirement: Bucket List
The system SHALL provide an OBJ management tab listing the names of all Object Store buckets
currently present on the connected server.

#### Scenario: Existing buckets are listed
- **WHEN** one or more OBJ buckets exist on the server
- **THEN** the OBJ tab's list shows each bucket's name

#### Scenario: No buckets exist
- **WHEN** no OBJ buckets exist on the server
- **THEN** the OBJ tab shows a non-interactive hint in place of the list, rather than a blank list

### Requirement: Bucket Detail Panel
The system SHALL show, alongside the bucket list, a detail panel for the currently highlighted
bucket, presenting at least its compression setting, object/message count, byte size, replica
count, and max age. Limit fields that carry a server "no limit" sentinel value SHALL render as
`(unlimited)` rather than their raw sentinel.

#### Scenario: Highlighting a bucket shows its details
- **WHEN** the user moves the highlight to a bucket in the list
- **THEN** the detail panel shows that bucket's stats

#### Scenario: No bucket highlighted
- **WHEN** the bucket list is empty and no bucket is highlighted
- **THEN** the detail panel shows no bucket's details

#### Scenario: Unlimited Max Age renders as unlimited
- **WHEN** the highlighted bucket's `MaxAge` is zero (the server's "no limit" sentinel)
- **THEN** the Max Age row shows `(unlimited)` rather than `00:00:00`

#### Scenario: A configured limit still renders as its value
- **WHEN** the highlighted bucket's Max Age is set to an actual positive limit rather than the
  unlimited sentinel
- **THEN** that row shows the configured value, unchanged from today's rendering

### Requirement: Periodic Bucket Detail Refresh
The system SHALL refresh the highlighted bucket's detail panel periodically while the OBJ tab is
the selected tab and the bucket level is shown, independent of any selection change. This refresh
SHALL apply only to the detail panel, not to the bucket list.

#### Scenario: Detail panel reflects a change made outside the app
- **WHEN** the OBJ tab is selected, a bucket is highlighted, and that bucket's object count changes
  on the server without any selection change in the app
- **THEN** the detail panel's shown object count updates within one refresh cycle

#### Scenario: Refresh does not run while the tab is not selected
- **WHEN** the OBJ tab is not the currently selected management tab
- **THEN** the system does not poll the server for bucket detail updates

### Requirement: Manual Bucket List Refresh
The system SHALL NOT automatically refresh the bucket list on a timer. The system SHALL allow the
user to refresh the bucket list on demand via Ctrl+R, re-fetching the set of buckets from the
server. If the previously-highlighted bucket is still present in the refreshed list, it SHALL
remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the OBJ tab is selected and a bucket is created or deleted on the server via another
  client, without the user pressing Ctrl+R
- **THEN** the bucket list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the list
- **WHEN** the user presses Ctrl+R while the bucket list holds focus
- **THEN** the bucket list is re-fetched from the server and the displayed list reflects any
  buckets created or deleted since the last fetch

### Requirement: Navigation Between Bucket and Object Levels
The system SHALL allow the user to descend from the bucket list into the highlighted bucket's
object list, and climb back up to the bucket list, with the current level always visually obvious.

#### Scenario: Enter descends into a bucket's objects
- **WHEN** the user presses Enter while a bucket is highlighted in the bucket list
- **THEN** the LHS list is replaced with that bucket's object list, and the RHS switches to
  tracking the highlighted object

#### Scenario: Esc climbs back to the bucket list
- **WHEN** the user presses Esc while viewing a bucket's object list
- **THEN** the LHS list is replaced with the bucket list, restored to its prior highlight and
  scroll position, and the RHS switches back to tracking the highlighted bucket

#### Scenario: Backspace climbs back to the bucket list
- **WHEN** the user presses Backspace while viewing a bucket's object list
- **THEN** the same result as pressing Esc occurs

#### Scenario: Current level is shown in the breadcrumb
- **WHEN** the user has descended into a bucket's objects
- **THEN** the LHS and RHS panel titles reflect the object level (e.g. naming the bucket) rather
  than the generic bucket-level titles

#### Scenario: Descending with no objects
- **WHEN** the user descends into a bucket that has no objects
- **THEN** the object list shows a non-interactive hint in place of the list, rather than a blank
  list

### Requirement: Object List
The system SHALL list the names of all non-deleted objects in the currently drilled-into bucket,
fetched fresh every time the user descends into that level.

#### Scenario: Descending fetches the object list
- **WHEN** the user descends into a bucket via Enter
- **THEN** the system fetches the current set of object names for that bucket from the server and
  displays them

#### Scenario: Re-descending re-fetches
- **WHEN** the user ascends from a bucket's object list and then descends into the same bucket
  again
- **THEN** the system fetches the object list again rather than reusing the previous result

#### Scenario: Ascending does not re-fetch the bucket list
- **WHEN** the user ascends from a bucket's object list back to the bucket list
- **THEN** the bucket list is not re-fetched from the server; it shows whatever it last held

#### Scenario: Deleted objects are excluded
- **WHEN** a bucket contains an object that has been deleted (tombstoned) on the server
- **THEN** that object does not appear in the object list

### Requirement: Manual Object List Refresh
The system SHALL NOT automatically refresh the object list on a timer. The system SHALL allow the
user to refresh the object list on demand via Ctrl+R while at the object level, re-fetching the
set of objects from the server. If the previously-highlighted object is still present in the
refreshed list, it SHALL remain highlighted; otherwise the first item in the refreshed list SHALL
become highlighted.

#### Scenario: The object list does not change on its own
- **WHEN** the object level is shown and an object is added or deleted on the server via another
  client, without the user pressing Ctrl+R
- **THEN** the object list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the object list
- **WHEN** the user presses Ctrl+R while the object level holds focus
- **THEN** the object list is re-fetched from the server and the displayed list reflects any
  objects added or deleted since the last fetch

### Requirement: Object Detail Panel Shows Metadata Only, Never Content
The system SHALL show, alongside the object list, a detail panel for the currently highlighted
object, presenting its description, size, chunk count, digest, and modified time. The system SHALL
NOT fetch, decode, or render the object's underlying content/bytes anywhere in this panel or
elsewhere in the OBJ tab.

#### Scenario: Highlighting an object shows its metadata
- **WHEN** the user moves the highlight to an object in the object list
- **THEN** the detail panel shows that object's description, size, chunk count, digest, and
  modified time

#### Scenario: Object content is never fetched
- **WHEN** an object is highlighted and its detail panel is shown or refreshed
- **THEN** the system does not issue any request to retrieve that object's content/bytes

#### Scenario: Highlighting an object fetches its details immediately, not on the next poll tick
- **WHEN** the user moves the highlight to an object in the object list
- **THEN** the system fetches that object's current metadata right away, rather than waiting for
  the periodic detail refresh interval to elapse — matching the immediacy of the bucket detail
  panel and the KV tab's key detail panel on their own highlight changes

#### Scenario: No object highlighted
- **WHEN** the object list is empty and no object is highlighted
- **THEN** the detail panel shows no object's details

### Requirement: Periodic Object Detail Refresh
The system SHALL refresh the highlighted object's detail panel periodically while the object level
is shown and the OBJ tab is the selected tab, independent of any selection change. This refresh
SHALL apply only to the detail panel, not to the object list.

#### Scenario: Detail panel reflects a change made outside the app
- **WHEN** the object level is shown, an object is highlighted, and that object's metadata changes
  on the server without any selection change in the app
- **THEN** the detail panel's shown metadata updates within one refresh cycle

#### Scenario: Refresh does not run while the bucket level is shown
- **WHEN** the user is viewing the bucket list (not drilled into a bucket's objects)
- **THEN** the system does not poll the server for object detail updates

#### Scenario: Refresh does not run while the tab is not selected
- **WHEN** the OBJ tab is not the currently selected management tab
- **THEN** the system does not poll the server for object detail updates, even if the object level
  was the last one shown

### Requirement: A Deleted Object Renders As No Selection
The system SHALL, when the highlighted object's detail panel refresh finds the object no longer
present on the server (deleted or otherwise not retrievable), render the detail panel identically
to how it renders when nothing is highlighted, without any distinct indication that the object
previously existed.

#### Scenario: An object deleted by another client while highlighted
- **WHEN** an object is highlighted in the object list, its details are shown, and that object is
  deleted on the server by another client before the next detail refresh
- **THEN** the next detail refresh clears the panel to the same empty state used when no object is
  highlighted

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

### Requirement: Delete Bucket
The system SHALL allow the user to delete the highlighted bucket from the bucket-level list via
Ctrl+D. Before deleting, the system SHALL prompt the user to confirm, naming the bucket to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the bucket on the server and refresh the bucket
list so the deleted bucket no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the bucket-level list holds focus and a bucket is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted bucket

#### Scenario: Confirming deletes the bucket
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the bucket on the server, and the bucket list is refreshed so the
  deleted bucket no longer appears

#### Scenario: Highlight moves to a neighboring bucket after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the bucket that was immediately below the deleted bucket becomes highlighted, or the
  bucket immediately above it if the deleted bucket was last in the list, or no bucket is
  highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no bucket is deleted and the bucket list is unchanged

#### Scenario: Delete is only reachable at the bucket level
- **WHEN** the OBJ tab is displayed at the object level (drilled into a bucket)
- **THEN** Ctrl+D has no effect on any bucket, since the bucket list is not displayed

#### Scenario: Server-side delete failure is reported without losing list state
- **WHEN** the user confirms the deletion prompt and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the bucket list is left unchanged after the dialog is dismissed

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
