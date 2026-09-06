## MODIFIED Requirements

### Requirement: Manual Bucket List Refresh
The system SHALL NOT automatically refresh the bucket list on a timer. The system SHALL allow the
user to refresh the bucket list on demand via R, re-fetching the set of buckets from the
server. If the previously-highlighted bucket is still present in the refreshed list, it SHALL
remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the Objects tab is selected and a bucket is created or deleted on the server via another
  client, without the user pressing R
- **THEN** the bucket list shown in the app does not change

#### Scenario: R re-fetches the list
- **WHEN** the user presses R while the bucket list holds focus
- **THEN** the bucket list is re-fetched from the server and the displayed list reflects any
  buckets created or deleted since the last fetch

### Requirement: Manual Object List Refresh
The system SHALL NOT automatically refresh the object list on a timer. The system SHALL allow the
user to refresh the object list on demand via R while at the object level, re-fetching the
set of objects from the server. If a post-fetch name filter is active, the refreshed list SHALL
remain filtered to that pattern (see "Post-Fetch Object Name Filter") rather than reverting to
showing every fetched name. If the previously-highlighted object is still present in the refreshed
(and, if applicable, filtered) list, it SHALL remain highlighted; otherwise the first item in the
refreshed list SHALL become highlighted.

#### Scenario: The object list does not change on its own
- **WHEN** the object level is shown and an object is added or deleted on the server via another
  client, without the user pressing R
- **THEN** the object list shown in the app does not change

#### Scenario: R re-fetches the object list
- **WHEN** the user presses R while the object level holds focus
- **THEN** the object list is re-fetched from the server and the displayed list reflects any
  objects added or deleted since the last fetch

#### Scenario: R with an active filter stays narrowed
- **WHEN** the user presses R while the object level holds focus and a post-fetch name filter
  is currently active
- **THEN** every object name is re-fetched as usual, but the displayed list remains narrowed to
  names matching the active filter

### Requirement: Post-Fetch Object Name Filter
The system SHALL allow the user, while at the object level, to set a name filter pattern via
F that narrows every subsequent object-list refresh for the currently drilled-into bucket —
descend, R, and any upload/delete-triggered refresh — to names matching that pattern, applied
to the full set of names fetched from the server (the fetch itself is never scoped; see "Object
List"), until the filter is cleared or the user ascends out of the bucket. Matching SHALL use the
same `* ? >` filter-expression grammar as every other list's F filter (see
`list-filter-affordance` and the `kv-filter-expression` capability), matched case-sensitively —
`*` matches any run of characters within one `.`-separated token, `?` matches exactly one non-`.`
character, and `>` matches one or more remaining tokens as a bare final segment (or an arbitrary
run of characters, including `.`, elsewhere). This is distinct from the existing live quick-search's
(`/`) fuzzy-subsequence matching. This filter is independent of `/`: the post-fetch filter narrows
what is retained after every fetch, `/` further narrows what is displayed from whatever the
post-fetch filter left, and both may be active at once.

#### Scenario: F opens the filter dialog
- **WHEN** the user presses F while the object-level list holds focus
- **THEN** a modal dialog opens with an editable pattern field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a valid, non-empty pattern sets the filter and refreshes
- **WHEN** the user enters a valid, non-empty pattern and confirms
- **THEN** that pattern becomes the active filter for the currently drilled-into bucket, and the
  object list is immediately re-fetched (every name) and re-narrowed to matches

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared, and the object list is immediately re-fetched and shown
  unfiltered (every name)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged, and no re-fetch occurs

#### Scenario: A wildcard pattern matches names case-sensitively
- **WHEN** the active filter pattern contains `*`, `?`, and/or `>` and an object's name matches
  that pattern per the filter-expression grammar
- **THEN** that object's name is retained after the post-fetch filter is applied

#### Scenario: A pattern without wildcards requires an exact, case-sensitive name match
- **WHEN** the active filter pattern contains no `*`, `?`, or `>` characters
- **THEN** only an object whose full name equals the pattern exactly, case-sensitively, is
  retained — unlike `/`'s fuzzy-subsequence search, a plain substring is not enough on its own

#### Scenario: A bare leading and/or trailing `*` matches within a token
- **WHEN** the active filter pattern is `*foo*`
- **THEN** any object name containing `foo` within a single `.`-free run is retained, per the same
  token-scoped `*` semantics every other list's filter uses

#### Scenario: The active filter persists across a plain refresh
- **WHEN** a filter is active and the user presses R, or performs an upload or delete that
  triggers an object-list refresh
- **THEN** the resulting fetch is re-narrowed by the active filter, rather than reverting to
  showing every fetched name

#### Scenario: The active filter resets on ascend
- **WHEN** a filter is active for the currently drilled-into bucket and the user ascends
  (Esc/Backspace) back to the bucket list
- **THEN** the filter is cleared, so a later descent into any bucket starts unfiltered

#### Scenario: The active filter resets on re-descend, even into the same bucket
- **WHEN** the user ascends out of a bucket that had an active filter and then descends into that
  same bucket again
- **THEN** the object list is fetched and shown unfiltered, not scoped to the previously active
  filter

#### Scenario: An active filter is shown in the object list's title
- **WHEN** a filter is active for the currently drilled-into bucket
- **THEN** the object list's title reflects that a filter is applied, distinguishing the view from
  "showing every object"

#### Scenario: No filter active leaves the title as today
- **WHEN** no filter is active for the currently drilled-into bucket
- **THEN** the object list's title matches its existing unfiltered form

#### Scenario: This filter never reduces what is fetched from the server
- **WHEN** any filter (active or not) is applied via this requirement
- **THEN** the underlying fetch from the server retrieves every non-deleted object name in the
  bucket regardless — the filter only affects what is retained/displayed afterward, never what is
  requested

### Requirement: Bucket List Filter
The system SHALL allow the user, while at the bucket level of the Objects tab, to set a filter
pattern via F that narrows the currently-loaded bucket list to names matching that pattern,
using the same `* ? >` filter-expression grammar as every other list's F filter (see
`list-filter-affordance`). Since the bucket list is always fetched in full (see "Bucket List"), this
filter narrows only what is displayed, never what is fetched, and persists across an R refresh
until cleared or explicitly changed.

#### Scenario: Confirming a valid pattern narrows the bucket list
- **WHEN** the user presses F while the bucket-level list holds focus, enters a valid,
  non-empty pattern, and confirms
- **THEN** only currently-loaded bucket names matching that pattern remain shown

#### Scenario: The bucket filter persists across a refresh
- **WHEN** a bucket-list filter is active and the user presses R
- **THEN** the refreshed bucket list is immediately narrowed by the still-active filter

### Requirement: Upload Object
The system SHALL allow the user to upload a local file as a new object in the currently
drilled-into bucket from the object-level list via N, which opens a modal dialog collecting
Key (the object's name) and a local file Path. On confirmation the system SHALL stream the local
file's contents to the server under the given Key and refresh the object list so the new object is
shown and highlighted.

#### Scenario: N opens the upload dialog
- **WHEN** the user presses N while the object-level list holds focus
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

#### Scenario: N has no effect at the bucket level
- **WHEN** the user presses N while the bucket-level list holds focus
- **THEN** the upload dialog does not open (N instead opens the create-bucket dialog, per
  "Create Bucket")

### Requirement: Download Object
The system SHALL allow the user to download the highlighted object's content to a local file from
the object-level list via S, which opens a modal dialog collecting the object's Key (shown but
disabled — the highlighted object's name) and a local file Path. On confirmation the system SHALL
stream the object's content from the server to the given local path.

#### Scenario: S opens the download dialog
- **WHEN** the user presses S while the object-level list holds focus and an object is
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

#### Scenario: S has no effect at the bucket level
- **WHEN** the user presses S while the bucket-level list holds focus
- **THEN** no download dialog opens

#### Scenario: S with no object highlighted does nothing
- **WHEN** the user presses S while the object-level list holds focus and the list is empty
  (no object highlighted)
- **THEN** no download dialog opens

### Requirement: Delete Object
The system SHALL allow the user to delete the highlighted object from the object-level list via
D. Before deleting, the system SHALL prompt the user to confirm, naming the object to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the object on the server and refresh the object
list so the deleted object no longer appears.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the object-level list holds focus and an object is
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
- **WHEN** the Objects tab is displayed at the object level (drilled into a bucket) and the user
  presses D
- **THEN** the highlighted object is deleted per this requirement, not the drilled-into bucket
  (bucket deletion, per "Delete Bucket", is only reachable at the bucket level)

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the object list is left unchanged until the next refresh

### Requirement: Create Bucket
The system SHALL allow the user to create a new Object Store bucket from the bucket-level list
via N, which opens a modal dialog collecting Name and Max Age. On confirmation the system
SHALL create the bucket on the server with the entered values and refresh the bucket list so the
new bucket is shown and highlighted. Fields not exposed in this dialog (description, max bytes,
storage backend, replica count, placement, metadata, compression, ...) SHALL be created with
safe, explicit defaults rather than left unset, so the resulting bucket is immediately usable —
storage backend in particular always defaults to file-backed storage, since this dialog offers no
way to choose memory-backed storage.

#### Scenario: N opens the create-bucket dialog
- **WHEN** the user presses N while the bucket-level list holds focus
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

### Requirement: Delete Bucket
The system SHALL allow the user to delete the highlighted bucket from the bucket-level list via
D. Before deleting, the system SHALL prompt the user to confirm, naming the bucket to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the bucket on the server and refresh the bucket
list so the deleted bucket no longer appears.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the bucket-level list holds focus and a bucket is
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
- **WHEN** the Objects tab is displayed at the object level (drilled into a bucket)
- **THEN** D has no effect on any bucket, since the bucket list is not displayed

#### Scenario: Server-side delete failure is reported without losing list state
- **WHEN** the user confirms the deletion prompt and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the bucket list is left unchanged after the dialog is dismissed

### Requirement: Edit Bucket
The system SHALL allow the user to edit the highlighted bucket from the bucket-level list via
E, which opens the same modal dialog used for "Create Bucket" in edit mode: the title and
confirm action read "Edit Bucket"/"Save", Name is shown but disabled (immutable on the server once
the bucket exists), and Max Age remains editable. On confirmation the system SHALL update the
bucket's maximum object age on the server, preserving every other aspect of the bucket's
server-side configuration unchanged, and refresh the bucket list so the updated bucket's detail
panel reflects the new value.

#### Scenario: E opens the edit-bucket dialog
- **WHEN** the user presses E while the bucket-level list holds focus and a bucket is
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

#### Scenario: E has no effect at the object level
- **WHEN** the user presses E while the object-level list holds focus
- **THEN** no edit-bucket dialog opens, since no bucket is displayed at that level

#### Scenario: E with no bucket highlighted does nothing
- **WHEN** the user presses E while the bucket-level list holds focus and the list is empty
  (no bucket highlighted)
- **THEN** no edit-bucket dialog opens
