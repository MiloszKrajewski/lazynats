## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Read-Only Tab
The OBJ tab's object level SHALL NOT provide any means to create, upload, edit, or delete an
object. The bucket level SHALL NOT provide any means to edit a bucket (creating and deleting a
bucket are provided — see "Create Bucket" and "Delete Bucket").

#### Scenario: No edit affordance is present at the bucket level
- **WHEN** the OBJ tab is displayed at the bucket level
- **THEN** no keybinding, button, or other control for editing a bucket is present

#### Scenario: No mutation affordance is present at the object level
- **WHEN** the OBJ tab is displayed at the object level (drilled into a bucket)
- **THEN** no keybinding, button, or other control for creating, uploading, editing, or deleting
  an object is present
