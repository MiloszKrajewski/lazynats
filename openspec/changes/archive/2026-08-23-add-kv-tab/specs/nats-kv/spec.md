## ADDED Requirements

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
The KV tab, at either the bucket level or the key level, SHALL NOT provide any means to create,
edit, or delete a bucket or key.

#### Scenario: No mutation affordance is present at the bucket level
- **WHEN** the KV tab is displayed at the bucket level
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a bucket is
  present

#### Scenario: No mutation affordance is present at the key level
- **WHEN** the KV tab is displayed at the key level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a key is
  present
