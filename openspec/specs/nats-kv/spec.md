# nats-kv Specification

## Purpose
TBD - created by syncing change add-kv-tab. Update Purpose after archive.

## Requirements
### Requirement: Bucket List
The system SHALL provide a KV management tab listing the names of all Key/Value store buckets
currently present on the connected server.

#### Scenario: Existing buckets are listed
- **WHEN** one or more KV buckets exist on the server
- **THEN** the KV tab's list shows each bucket's name

#### Scenario: No buckets exist
- **WHEN** no KV buckets exist on the server
- **THEN** the KV tab shows a non-interactive hint in place of the list, rather than a blank list

### Requirement: Bucket Detail Panel
The system SHALL show, alongside the bucket list, a detail panel for the currently highlighted
bucket, presenting at least its compression setting, TTL-marker limit, entry count, byte size, and
history depth.

#### Scenario: Highlighting a bucket shows its details
- **WHEN** the user moves the highlight to a bucket in the list
- **THEN** the detail panel shows that bucket's stats

#### Scenario: No bucket highlighted
- **WHEN** the bucket list is empty and no bucket is highlighted
- **THEN** the detail panel shows no bucket's details

### Requirement: Periodic Bucket Detail Refresh
The system SHALL refresh the highlighted bucket's detail panel periodically while the KV tab is
the selected tab and the bucket level is shown, independent of any selection change. This refresh
SHALL apply only to the detail panel, not to the bucket list.

#### Scenario: Detail panel reflects a change made outside the app
- **WHEN** the KV tab is selected, a bucket is highlighted, and that bucket's entry count changes
  on the server without any selection change in the app
- **THEN** the detail panel's shown entry count updates within one refresh cycle

#### Scenario: Refresh does not run while the tab is not selected
- **WHEN** the KV tab is not the currently selected management tab
- **THEN** the system does not poll the server for bucket detail updates

### Requirement: Manual Bucket List Refresh
The system SHALL NOT automatically refresh the bucket list on a timer. The system SHALL allow the
user to refresh the bucket list on demand via Ctrl+R, re-fetching the set of buckets from the
server. If the previously-highlighted bucket is still present in the refreshed list, it SHALL
remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the KV tab is selected and a bucket is created or deleted on the server via another
  client, without the user pressing Ctrl+R
- **THEN** the bucket list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the list
- **WHEN** the user presses Ctrl+R while the bucket list holds focus
- **THEN** the bucket list is re-fetched from the server and the displayed list reflects any
  buckets created or deleted since the last fetch

### Requirement: Navigation Between Bucket and Key Levels
The system SHALL allow the user to descend from the bucket list into the highlighted bucket's key
list, and climb back up to the bucket list, with the current level always visually obvious.

#### Scenario: Enter descends into a bucket's keys
- **WHEN** the user presses Enter while a bucket is highlighted in the bucket list
- **THEN** the LHS list is replaced with that bucket's key list, and the RHS switches to tracking
  the highlighted key

#### Scenario: Esc climbs back to the bucket list
- **WHEN** the user presses Esc while viewing a bucket's key list
- **THEN** the LHS list is replaced with the bucket list, restored to its prior highlight and
  scroll position, and the RHS switches back to tracking the highlighted bucket

#### Scenario: Backspace climbs back to the bucket list
- **WHEN** the user presses Backspace while viewing a bucket's key list
- **THEN** the same result as pressing Esc occurs

#### Scenario: Current level is shown in the breadcrumb
- **WHEN** the user has descended into a bucket's keys
- **THEN** the LHS and RHS panel titles reflect the key level (e.g. naming the bucket) rather than
  the generic bucket-level titles

#### Scenario: Descending with no keys
- **WHEN** the user descends into a bucket that has no keys
- **THEN** the key list shows a non-interactive hint in place of the list, rather than a blank list

### Requirement: Key List
The system SHALL list the names of all keys in the currently drilled-into bucket, fetched fresh
every time the user descends into that level.

#### Scenario: Descending fetches the key list
- **WHEN** the user descends into a bucket via Enter
- **THEN** the system fetches the current set of key names for that bucket from the server and
  displays them

#### Scenario: Re-descending re-fetches
- **WHEN** the user ascends from a bucket's key list and then descends into the same bucket again
- **THEN** the system fetches the key list again rather than reusing the previous result

#### Scenario: Ascending does not re-fetch the bucket list
- **WHEN** the user ascends from a bucket's key list back to the bucket list
- **THEN** the bucket list is not re-fetched from the server; it shows whatever it last held

### Requirement: Manual Key List Refresh
The system SHALL NOT automatically refresh the key list on a timer. The system SHALL allow the
user to refresh the key list on demand via Ctrl+R while at the key level, re-fetching the set of
keys from the server. If the previously-highlighted key is still present in the refreshed list, it
SHALL remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The key list does not change on its own
- **WHEN** the key level is shown and a key is created or deleted on the server via another
  client, without the user pressing Ctrl+R
- **THEN** the key list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the key list
- **WHEN** the user presses Ctrl+R while the key level holds focus
- **THEN** the key list is re-fetched from the server and the displayed list reflects any keys
  created or deleted since the last fetch

### Requirement: Key Detail Panel
The system SHALL show, alongside the key list, a detail panel for the currently highlighted key,
presenting its revision, creation time, and operation kind, together with its value rendered as
UTF-8 text filling the remainder of the panel's available space.

#### Scenario: Highlighting a key shows its details
- **WHEN** the user moves the highlight to a key in the key list
- **THEN** the detail panel shows that key's revision, creation time, operation, and its value
  decoded as UTF-8 text

#### Scenario: Highlighting a key fetches its details immediately, not on the next poll tick
- **WHEN** the user moves the highlight to a key in the key list
- **THEN** the system fetches that key's current entry right away, rather than waiting for the
  periodic detail refresh interval to elapse — matching the immediacy of the bucket, stream, and
  consumer detail panels on their own highlight changes

#### Scenario: No key highlighted
- **WHEN** the key list is empty and no key is highlighted
- **THEN** the detail panel shows no key's details

#### Scenario: Value occupies remaining panel space
- **WHEN** a key's details are shown
- **THEN** the value text is rendered in the space remaining below the revision/creation/operation
  rows, rather than being constrained to a single line among them

#### Scenario: A value taller than the available space is clipped, not scrolled
- **WHEN** a key's value, once rendered, would require more vertical space than the detail panel
  currently has available
- **THEN** the panel shows as much of the value as fits and does not offer scrolling to reveal the
  rest

### Requirement: Periodic Key Detail Refresh
The system SHALL refresh the highlighted key's detail panel periodically while the key level is
shown and the KV tab is the selected tab, independent of any selection change. This refresh SHALL
apply only to the detail panel, not to the key list.

#### Scenario: Detail panel reflects a change made outside the app
- **WHEN** the key level is shown, a key is highlighted, and that key's value changes on the
  server without any selection change in the app
- **THEN** the detail panel's shown value updates within one refresh cycle

#### Scenario: Refresh does not run while the bucket level is shown
- **WHEN** the user is viewing the bucket list (not drilled into a bucket's keys)
- **THEN** the system does not poll the server for key detail updates

#### Scenario: Refresh does not run while the tab is not selected
- **WHEN** the KV tab is not the currently selected management tab
- **THEN** the system does not poll the server for key detail updates, even if the key level was
  the last one shown

### Requirement: A Deleted Key Renders As No Selection
The system SHALL, when the highlighted key's detail panel refresh finds the key no longer present
on the server (deleted, purged, or otherwise not retrievable), render the detail panel identically
to how it renders when nothing is highlighted, without any distinct indication that the key
previously existed.

#### Scenario: A key deleted by another client while highlighted
- **WHEN** a key is highlighted in the key list, its details are shown, and that key is deleted on
  the server by another client before the next detail refresh
- **THEN** the next detail refresh clears the panel to the same empty state used when no key is
  highlighted

### Requirement: Read-Only Tab
The KV tab's key level SHALL NOT provide any means to create, edit, or delete a key. The bucket
level SHALL NOT provide any means to edit a bucket (creating and deleting a bucket are both
provided — see "Create Bucket" and "Delete Bucket").

#### Scenario: No edit affordance is present at the bucket level
- **WHEN** the KV tab is displayed at the bucket level
- **THEN** no keybinding, button, or other control for editing a bucket is present

#### Scenario: No mutation affordance is present at the key level
- **WHEN** the KV tab is displayed at the key level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a key is
  present

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
- **WHEN** the KV tab is displayed at the key level (drilled into a bucket)
- **THEN** Ctrl+D has no effect on any bucket, since the bucket list is not displayed

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the bucket list is left unchanged until the next refresh

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
