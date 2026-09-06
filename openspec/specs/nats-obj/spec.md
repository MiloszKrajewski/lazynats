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
count, and max age.

#### Scenario: Highlighting a bucket shows its details
- **WHEN** the user moves the highlight to a bucket in the list
- **THEN** the detail panel shows that bucket's stats

#### Scenario: No bucket highlighted
- **WHEN** the bucket list is empty and no bucket is highlighted
- **THEN** the detail panel shows no bucket's details

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

### Requirement: Read-Only Tab
The OBJ tab, at either the bucket level or the object level, SHALL NOT provide any means to
create, upload, edit, or delete a bucket or object.

#### Scenario: No mutation affordance is present at the bucket level
- **WHEN** the OBJ tab is displayed at the bucket level
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a bucket is
  present

#### Scenario: No mutation affordance is present at the object level
- **WHEN** the OBJ tab is displayed at the object level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, uploading, editing, or deleting an
  object is present
