## MODIFIED Requirements

### Requirement: Manual Bucket List Refresh
The system SHALL NOT automatically refresh the bucket list on a timer. The system SHALL allow the
user to refresh the bucket list on demand via R, re-fetching the set of buckets from the
server. If the previously-highlighted bucket is still present in the refreshed list, it SHALL
remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the Values tab is selected and a bucket is created or deleted on the server via another
  client, without the user pressing R
- **THEN** the bucket list shown in the app does not change

#### Scenario: R re-fetches the list
- **WHEN** the user presses R while the bucket list holds focus
- **THEN** the bucket list is re-fetched from the server and the displayed list reflects any
  buckets created or deleted since the last fetch

### Requirement: Manual Key List Refresh
The system SHALL NOT automatically refresh the key list on a timer. The system SHALL allow the
user to refresh the key list on demand via R while at the key level, re-fetching the set of
keys from the server. If a server-side key filter is active, the refresh fetch SHALL remain scoped
to that filter (see "Server-Side Key Filter") rather than reverting to fetching every key. If the
previously-highlighted key is still present in the refreshed list, it SHALL remain highlighted;
otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The key list does not change on its own
- **WHEN** the key level is shown and a key is created or deleted on the server via another
  client, without the user pressing R
- **THEN** the key list shown in the app does not change

#### Scenario: R re-fetches the key list
- **WHEN** the user presses R while the key level holds focus
- **THEN** the key list is re-fetched from the server and the displayed list reflects any keys
  created or deleted since the last fetch

#### Scenario: R with an active filter stays scoped
- **WHEN** the user presses R while the key level holds focus and a server-side key filter is
  currently active
- **THEN** the key list is re-fetched scoped to that filter's pattern, not every key in the bucket

### Requirement: Server-Side Key Filter
The system SHALL allow the user, while at the key level, to set a filter expression (see the
`kv-filter-expression` capability for its grammar) via F that scopes every subsequent
key-list fetch for the currently drilled-into bucket — descend, R, and any create/edit-
triggered refresh — to keys matching that expression, until the filter is cleared or the user
ascends out of the bucket. Matching is resolved by compiling the expression into a native NATS
subject filter (used to scope the server-side fetch) plus, only when the expression needs it, a
client-side regex applied to the fetch's results — which phase(s) actually ran is not observable
from the key list beyond the final matching set it shows. This is independent of the existing
in-memory quick-search (`/`): the server-side filter narrows what is fetched from the server, the
quick-search narrows what is displayed from whatever was fetched, and both may be active at once.
Every key-list fetch SHALL stop after collecting 10,000 matching keys, regardless of how many more
the native-filter-scoped fetch could have returned — including an unfiltered fetch, which is
scoped by the same mechanism using a native filter that matches every key.

#### Scenario: F opens the filter dialog
- **WHEN** the user presses F while the key-level list holds focus
- **THEN** a modal dialog opens with an editable expression field, seeded with the currently active
  filter expression, or empty if no filter is active

#### Scenario: An invalid expression cannot be confirmed
- **WHEN** the user types a filter expression containing an empty token (a leading, trailing, or
  doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming a non-empty, valid expression sets the filter and refreshes
- **WHEN** the user enters a non-empty, valid filter expression and confirms
- **THEN** that expression becomes the active filter for the currently drilled-into bucket, and the
  key list is immediately re-fetched scoped to it

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared, and the key list is immediately re-fetched unscoped
  (every key in the bucket)

#### Scenario: Cancelling the dialog leaves the filter unchanged
- **WHEN** the user opens the filter dialog and cancels (Esc) instead of confirming
- **THEN** the active filter (or lack of one) is unchanged, and no re-fetch occurs

#### Scenario: Only keys matching the full expression are shown
- **WHEN** the active filter expression requires a client-side regex phase (e.g. it contains `?`,
  or a `>` outside its native terminal position)
- **THEN** the key list shows only keys matching the expression exactly, not every key the
  native-filter-scoped fetch happened to return

#### Scenario: The active filter persists across a plain refresh
- **WHEN** a filter is active and the user presses R, or performs a create or edit that
  triggers a key-list refresh
- **THEN** the resulting fetch remains scoped to the active filter, rather than reverting to
  fetching every key in the bucket

#### Scenario: The active filter resets on ascend
- **WHEN** a filter is active for the currently drilled-into bucket and the user ascends
  (Esc/Backspace) back to the bucket list
- **THEN** the filter is cleared, so a later descent into any bucket starts unfiltered

#### Scenario: The active filter resets on re-descend, even into the same bucket
- **WHEN** the user ascends out of a bucket that had an active filter and then descends into that
  same bucket again
- **THEN** the key list is fetched unfiltered (every key), not scoped to the previously active
  filter

#### Scenario: A filtered fetch stops at 10,000 matching keys
- **WHEN** an active filter's matching keys in the currently drilled-into bucket number more than
  10,000
- **THEN** the key-list fetch stops after collecting 10,000 matches, rather than continuing to
  collect every match in the bucket

#### Scenario: An unfiltered fetch is capped the same way
- **WHEN** no filter is active for the currently drilled-into bucket, and the bucket holds more
  than 10,000 keys
- **THEN** the key-list fetch stops after collecting 10,000 keys, the same as a filtered fetch that
  hits the cap

#### Scenario: An active filter is shown in the key list's title
- **WHEN** a filter is active for the currently drilled-into bucket and its matches did not hit the
  10,000-key cap
- **THEN** the key list's title reflects that a filter is applied, distinguishing the view from
  "showing every key"

#### Scenario: A capped fetch is indicated in the key list's title
- **WHEN** a fetch (filtered or unfiltered) stops at the 10,000-key cap
- **THEN** the key list's title indicates that the shown keys are a truncated subset, distinct from
  the plain "filter applied" indication - even with no active filter, so a capped unfiltered view
  is never mistaken for a complete one

#### Scenario: No filter active and no cap hit leaves the title as today
- **WHEN** no filter is active for the currently drilled-into bucket and its key count does not hit
  the 10,000-key cap
- **THEN** the key list's title matches its existing unfiltered form

#### Scenario: A server-side fetch failure is reported
- **WHEN** the filtered (or newly unfiltered) key-list fetch fails
- **THEN** the system reports the failure the same way any other key-list fetch failure is
  reported, and the key list is left showing whatever it last held

### Requirement: Bucket List Filter
The system SHALL allow the user, while at the bucket level of the Values tab, to set a filter
pattern via F that narrows the currently-loaded bucket list to names matching that pattern,
using the same `* ? >` filter-expression grammar as every other list's F filter (see
`list-filter-affordance`). Since the bucket list is always fetched in full (see "Bucket List"),
this filter narrows only what is displayed, never what is fetched, and persists across an R
refresh until cleared or explicitly changed. This is distinct from the key level's existing
server-side key filter ("Server-Side Key Filter"), which scopes a fetch rather than only narrowing
a display — the bucket level has no server-side fetch to scope.

#### Scenario: Confirming a valid pattern narrows the bucket list
- **WHEN** the user presses F while the bucket-level list holds focus, enters a valid,
  non-empty pattern, and confirms
- **THEN** only currently-loaded bucket names matching that pattern remain shown

#### Scenario: The bucket filter persists across a refresh
- **WHEN** a bucket-list filter is active and the user presses R
- **THEN** the refreshed bucket list is immediately narrowed by the still-active filter

### Requirement: Create Key
The system SHALL allow the user to create a new key/value entry from the key-level list via
N, which opens a modal dialog collecting Name and Value (a multi-line text field). On
confirmation the system SHALL write the entry to the currently drilled-into bucket on the server
and refresh the key list so the new key is shown and highlighted.

#### Scenario: N opens the create-key dialog
- **WHEN** the user presses N while the key-level list holds focus
- **THEN** a modal dialog opens with an editable Name field and a multi-line Value field, both
  empty

#### Scenario: Confirming a valid dialog creates the key
- **WHEN** the user fills in a non-empty Name, optionally enters a Value (including leaving it
  empty, or entering multiple lines of text), and confirms (Create)
- **THEN** the system writes the entry to the currently drilled-into bucket, the dialog closes,
  and the key list is refreshed with the new key shown and highlighted

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-key dialog and cancels (Esc) instead of confirming
- **THEN** no key is created and the key list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the write (e.g. invalid key name)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-key dialog reopens with the previously entered Name and
  Value still filled in

#### Scenario: N has no effect at the bucket level
- **WHEN** the user presses N while the bucket-level list holds focus
- **THEN** the create-key dialog does not open (N instead opens the create-bucket dialog, per
  "Create Bucket")

### Requirement: Edit Key
The system SHALL allow the user to edit the highlighted key's value from the key-level list via
E, which opens the same modal dialog used for "Create Key" in edit mode: the title and
confirm action read "Edit Key"/"Save", Name is shown but disabled (immutable once the entry
exists), and Value is seeded with the key's current value and remains editable. On confirmation
the system SHALL overwrite the entry on the server with the edited Value and refresh the key list
so the updated key's detail panel reflects the new value.

#### Scenario: E opens the edit-key dialog
- **WHEN** the user presses E while the key-level list holds focus, a key is highlighted, and
  that key's current value passes the printable-text guard (see "Edit Key Printable-Text Guard")
- **THEN** a modal dialog opens, seeded with that key's Name and current Value (decoded as UTF-8
  text), with its title and confirm button reading "Edit"/"Save"

#### Scenario: Name is locked
- **WHEN** the edit-key dialog is open
- **THEN** the Name field shows the key's current name but cannot be changed

#### Scenario: Confirming updates the value
- **WHEN** the user changes Value and confirms (Save)
- **THEN** the system overwrites the entry's value on the server, the dialog closes, and the key
  list is refreshed with the updated key highlighted

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-key dialog and cancels (Esc) instead of confirming
- **THEN** the key is not updated and the key list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the update
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-key dialog reopens with the previously entered Value still
  filled in

#### Scenario: E has no effect at the bucket level
- **WHEN** the user presses E while the bucket-level list holds focus
- **THEN** no edit-key dialog opens (E instead opens the edit-bucket dialog, per "Edit
  Bucket")

#### Scenario: E with no key highlighted does nothing
- **WHEN** the user presses E while the key-level list holds focus and the list is empty (no
  key highlighted)
- **THEN** no edit-key dialog opens

### Requirement: Edit Key Printable-Text Guard
The system SHALL refuse to open the edit-key dialog for a key whose current value, freshly
fetched from the server, is not valid, printable UTF-8 text (invalid UTF-8, or containing a
control character other than tab/newline/carriage-return). Instead it SHALL report the refusal as
a status message and leave the key list and the key's value unchanged. This guard applies only to
Edit — Create is never subject to it, since a value typed into the create-key dialog is always
text by construction.

#### Scenario: Editing a key with a binary value is refused
- **WHEN** the user presses E on a key whose current value is not valid printable UTF-8 text
- **THEN** the system shows a status message explaining the value can't be edited as text, and no
  dialog opens

#### Scenario: Editing a key with a printable text value proceeds normally
- **WHEN** the user presses E on a key whose current value is valid printable UTF-8 text
- **THEN** the edit-key dialog opens as described in "Edit Key"

#### Scenario: The guard checks the value fresh, not the last polled detail-panel value
- **WHEN** the user presses E on a highlighted key
- **THEN** the system fetches that key's current entry from the server before deciding whether to
  open the dialog, rather than relying on whatever the detail panel last polled

### Requirement: Delete Key
The system SHALL allow the user to delete the highlighted key from the key-level list via D.
Before deleting, the system SHALL prompt the user to confirm, naming the key to be deleted, with
the non-destructive choice (Cancel) as the prompt's default (Enter-activated) response. On
confirmation the system SHALL delete the key on the server (a tombstoning delete, not a
history-purging one) and refresh the key list so the deleted key no longer appears.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the key-level list holds focus and a key is highlighted
- **THEN** a confirmation prompt opens, naming the highlighted key

#### Scenario: Confirming deletes the key
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the key on the server, and the key list is refreshed so the deleted
  key no longer appears

#### Scenario: Highlight moves to a neighboring key after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the key that was immediately below the deleted key becomes highlighted, or the key
  immediately above it if the deleted key was last in the list, or no key is highlighted if the
  list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no key is deleted and the key list is unchanged

#### Scenario: Delete is only reachable at the key level
- **WHEN** the Values tab is displayed at the bucket level
- **THEN** D instead deletes the highlighted bucket, per "Delete Bucket" — no key-level
  delete is reachable since no key list is displayed

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the key list is left unchanged until the next refresh

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
- **WHEN** the Values tab is displayed at the key level (drilled into a bucket)
- **THEN** D has no effect on any bucket, since the bucket list is not displayed

### Requirement: Create Bucket
The system SHALL allow the user to create a new KV bucket from the bucket-level list via N,
which opens a modal dialog collecting Name, Storage backend, History, Max Age, and Limit Marker
TTL. On confirmation the system SHALL create the bucket on the server with the entered values and
refresh the bucket list so the new bucket is shown and highlighted. Fields not exposed in this
dialog (max value size, max bytes, replica count, compression, republish, placement,
mirrors/sources, metadata, ...) SHALL be created with safe, explicit defaults rather than left
unset, so the resulting bucket is immediately usable.

#### Scenario: N opens the create-bucket dialog
- **WHEN** the user presses N while the bucket-level list holds focus
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

### Requirement: Edit Bucket
The system SHALL allow the user to edit the highlighted bucket from the bucket-level list via
E, which opens the same modal dialog used for "Create Bucket" in edit mode: the title and
confirm action read "Edit Bucket"/"Save", Name and Storage are shown but disabled (both are
immutable on the server once the bucket exists), and History, Max Age, and Limit Marker TTL remain
editable. On confirmation the system SHALL update the bucket on the server with the edited values,
preserving every field the dialog does not expose (max value size, max bytes, replica count,
compression, republish, placement, mirrors/sources, metadata, ...) unchanged from the bucket's
current server-side configuration, and refresh the bucket list so the updated bucket's detail
panel reflects the new values.

#### Scenario: E opens the edit-bucket dialog
- **WHEN** the user presses E while the bucket-level list holds focus and a bucket is
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

#### Scenario: E has no effect at the key level
- **WHEN** the user presses E while the key-level list holds focus
- **THEN** no edit-bucket dialog opens, since no bucket is displayed at that level

#### Scenario: E with no bucket highlighted does nothing
- **WHEN** the user presses E while the bucket-level list holds focus and the list is empty
  (no bucket highlighted)
- **THEN** no edit-bucket dialog opens
