## REMOVED Requirements

### Requirement: Read-Only Tab
**Reason**: The key level gains Create/Edit/Delete affordances (see "Create Key", "Edit Key",
"Delete Key" below), mirroring the bucket level's own CRUD shape.
**Migration**: No user action needed. Existing keys are unaffected; the key level simply gains
Ctrl+N/Ctrl+E/Ctrl+D, matching the bucket level.

The KV tab's key level SHALL NOT provide any means to create, edit, or delete a key. Creating,
editing, and deleting a bucket are all provided at the bucket level — see "Create Bucket", "Edit
Bucket", and "Delete Bucket".

#### Scenario: No mutation affordance is present at the key level
- **WHEN** the KV tab is displayed at the key level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a key is
  present

## ADDED Requirements

### Requirement: Create Key
The system SHALL allow the user to create a new key/value entry from the key-level list via
Ctrl+N, which opens a modal dialog collecting Name and Value (a multi-line text field). On
confirmation the system SHALL write the entry to the currently drilled-into bucket on the server
and refresh the key list so the new key is shown and highlighted.

#### Scenario: Ctrl+N opens the create-key dialog
- **WHEN** the user presses Ctrl+N while the key-level list holds focus
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

#### Scenario: Ctrl+N has no effect at the bucket level
- **WHEN** the user presses Ctrl+N while the bucket-level list holds focus
- **THEN** the create-key dialog does not open (Ctrl+N instead opens the create-bucket dialog, per
  "Create Bucket")

### Requirement: Create Key Field Validation
The create-key dialog SHALL validate Name before allowing confirmation, and SHALL visually flag
an invalid Name rather than allowing a request that will fail immediately. Value has no client-
side validation — any text, including empty text, is a valid Value.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty Value is allowed
- **WHEN** the Value field is left empty and Name is valid
- **THEN** the Create action is available and the created key's value is an empty string

### Requirement: Edit Key
The system SHALL allow the user to edit the highlighted key's value from the key-level list via
Ctrl+E, which opens the same modal dialog used for "Create Key" in edit mode: the title and
confirm action read "Edit Key"/"Save", Name is shown but disabled (immutable once the entry
exists), and Value is seeded with the key's current value and remains editable. On confirmation
the system SHALL overwrite the entry on the server with the edited Value and refresh the key list
so the updated key's detail panel reflects the new value.

#### Scenario: Ctrl+E opens the edit-key dialog
- **WHEN** the user presses Ctrl+E while the key-level list holds focus, a key is highlighted, and
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

#### Scenario: Ctrl+E has no effect at the bucket level
- **WHEN** the user presses Ctrl+E while the bucket-level list holds focus
- **THEN** no edit-key dialog opens (Ctrl+E instead opens the edit-bucket dialog, per "Edit
  Bucket")

#### Scenario: Ctrl+E with no key highlighted does nothing
- **WHEN** the user presses Ctrl+E while the key-level list holds focus and the list is empty (no
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
- **WHEN** the user presses Ctrl+E on a key whose current value is not valid printable UTF-8 text
- **THEN** the system shows a status message explaining the value can't be edited as text, and no
  dialog opens

#### Scenario: Editing a key with a printable text value proceeds normally
- **WHEN** the user presses Ctrl+E on a key whose current value is valid printable UTF-8 text
- **THEN** the edit-key dialog opens as described in "Edit Key"

#### Scenario: The guard checks the value fresh, not the last polled detail-panel value
- **WHEN** the user presses Ctrl+E on a highlighted key
- **THEN** the system fetches that key's current entry from the server before deciding whether to
  open the dialog, rather than relying on whatever the detail panel last polled

### Requirement: Delete Key
The system SHALL allow the user to delete the highlighted key from the key-level list via Ctrl+D.
Before deleting, the system SHALL prompt the user to confirm, naming the key to be deleted, with
the non-destructive choice (Cancel) as the prompt's default (Enter-activated) response. On
confirmation the system SHALL delete the key on the server (a tombstoning delete, not a
history-purging one) and refresh the key list so the deleted key no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the key-level list holds focus and a key is highlighted
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
- **WHEN** the KV tab is displayed at the bucket level
- **THEN** Ctrl+D instead deletes the highlighted bucket, per "Delete Bucket" — no key-level
  delete is reachable since no key list is displayed

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the key list is left unchanged until the next refresh
