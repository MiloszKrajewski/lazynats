## MODIFIED Requirements

### Requirement: Manual Template List Refresh
The system SHALL NOT automatically refresh the template list on a timer. The system SHALL allow
the user to refresh it on demand via R, re-fetching the set of templates from the bucket. If
the previously-highlighted template is still present in the refreshed list, it SHALL remain
highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the Templates tab is selected and a template is added, edited, or removed in the bucket
  via another client, without the user pressing R
- **THEN** the template list shown in the app does not change

#### Scenario: R re-fetches the list
- **WHEN** the user presses R while the Templates list holds focus
- **THEN** the list is re-fetched from the `lazynats-templates` bucket and reflects any templates
  added or removed since the last fetch

### Requirement: Create Template
The system SHALL allow the user to create a new template via N, opening a modal dialog
collecting Name, Subject, Headers (key/value pairs), Payload Type (`Json`, `Text`, `Base64`, or
`Hex`, defaulting to `Text`), and Payload (multi-line text). On confirmation the system SHALL
ensure the `lazynats-templates` bucket exists (creating it with safe defaults if it does not),
write the new entry, and refresh the template list so the new template is shown and highlighted.

#### Scenario: N opens the create-template dialog
- **WHEN** the user presses N while the Templates list holds focus
- **THEN** a modal dialog opens with empty Name, Subject, Headers, and Payload fields, and Payload
  Type defaulted to `Text`

#### Scenario: The Headers field has no filter/search of its own
- **WHEN** the create-template (or edit-template) dialog is open
- **THEN** its Headers field offers no quick-search ("/") or filter (F) of its own, and is at
  least 3 lines tall regardless of how many headers it currently holds

#### Scenario: Confirming a valid dialog creates the template
- **WHEN** the user fills in a valid Name and Subject, optionally adds Headers, selects a Payload
  Type, enters a Payload valid for that type, and confirms (Create)
- **THEN** the system writes the entry to the `lazynats-templates` bucket (creating the bucket
  first if it did not already exist), the dialog closes, and the template list is refreshed with
  the new template shown and highlighted

#### Scenario: Creating the first template creates the bucket
- **WHEN** the `lazynats-templates` bucket does not exist and the user successfully creates a
  template
- **THEN** the bucket exists afterward, containing that template's entry

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-template dialog and cancels (Esc) instead of confirming
- **THEN** no template is created and the template list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the write to the bucket fails
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-template dialog reopens with the previously entered
  values still filled in

### Requirement: Edit Template
The system SHALL allow the user to edit the highlighted template via E, opening the same
modal dialog used for "Create Template" in edit mode: the title and confirm action read "Edit
Template"/"Save", Name is shown but disabled (immutable once the template exists, since it is the
KV key), and Subject, Headers, Payload Type, and Payload are seeded with the template's current
values and remain editable. On confirmation the system SHALL overwrite the entry in the
`lazynats-templates` bucket and refresh the template list so the updated template's row reflects
any changed content.

#### Scenario: E opens the edit-template dialog
- **WHEN** the user presses E while the Templates list holds focus and a template is
  highlighted
- **THEN** a modal dialog opens, seeded with that template's current Name, Subject, Headers,
  Payload Type, and Payload, with its title and confirm button reading "Edit"/"Save"

#### Scenario: Name is locked
- **WHEN** the edit-template dialog is open
- **THEN** the Name field shows the template's current name but cannot be changed

#### Scenario: Confirming updates the template
- **WHEN** the user changes any of Subject, Headers, Payload Type, or Payload and confirms (Save)
- **THEN** the system overwrites the entry in the `lazynats-templates` bucket, the dialog closes,
  and the template list is refreshed with the updated template highlighted

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-template dialog and cancels (Esc) instead of confirming
- **THEN** the template is not updated and the template list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the write to the bucket fails
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-template dialog reopens with the previously entered values
  still filled in

#### Scenario: E with no template highlighted does nothing
- **WHEN** the user presses E while the Templates list holds focus and the list is empty (no
  template highlighted)
- **THEN** no edit-template dialog opens

### Requirement: Delete Template
The system SHALL allow the user to delete the highlighted template via D. Before deleting,
the system SHALL prompt the user to confirm, naming the template to be deleted, with the
non-destructive choice (Cancel) as the prompt's default (Enter-activated) response. On
confirmation the system SHALL delete the entry from the `lazynats-templates` bucket and refresh
the template list so the deleted template no longer appears.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the Templates list holds focus and a template is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted template

#### Scenario: Confirming deletes the template
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the entry from the `lazynats-templates` bucket, and the template
  list is refreshed so the deleted template no longer appears

#### Scenario: Highlight moves to a neighboring template after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the template that was immediately below the deleted template becomes highlighted, or
  the template immediately above it if the deleted template was last in the list, or no template
  is highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no template is deleted and the template list is unchanged

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms deletion and the delete fails
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the template list is left unchanged until the next refresh

### Requirement: Template List Quick-Search and Filter
The system SHALL offer the same shared, in-memory quick-search (`/`) and filter (F,
`* ? >` expression grammar per `list-filter-affordance`) wiring `drillable-list` already specifies,
applied to template names, with both narrowing only what is displayed - never what is fetched or
which template Edit/Delete act on.

#### Scenario: Quick-search narrows the displayed templates
- **WHEN** the user presses `/` and types a query matching some, but not all, template names as a
  case-insensitive subsequence
- **THEN** only the matching templates remain shown

#### Scenario: Filter narrows the displayed templates
- **WHEN** the user presses F, enters a valid, non-empty pattern, and confirms
- **THEN** only template names matching that pattern remain shown

#### Scenario: Edit and Delete act on the correct template regardless of filtering
- **WHEN** quick-search or the filter narrows the displayed templates, the user selects one of the
  displayed templates, and presses E or D
- **THEN** the operation acts on that same selected template, not a different one

### Requirement: Export Templates
The system SHALL allow the user to export every template currently in the `lazynats-templates`
bucket via X while the Templates tab holds focus, writing them as a single JSON object to a
local file the user picks via a native file-save dialog. The object SHALL be keyed by template
name, each value carrying that template's Subject, Headers, Payload Type, and Payload in the same
shape the bucket stores them in (see the "Template Storage" requirement).

#### Scenario: X opens a file-save dialog
- **WHEN** the user presses X while the Templates tab holds focus
- **THEN** a native file-save dialog opens for choosing the destination file

#### Scenario: Confirming the dialog writes every template
- **WHEN** the user confirms the file-save dialog with a destination path
- **THEN** the chosen file is written containing every template currently in
  `lazynats-templates`, one JSON object entry per template keyed by name

#### Scenario: Exporting with no templates writes an empty object
- **WHEN** the `lazynats-templates` bucket contains no templates and the user confirms Export
- **THEN** the chosen file is written containing an empty JSON object

#### Scenario: Cancelling the dialog exports nothing
- **WHEN** the user opens the file-save dialog via X and cancels it instead of confirming
- **THEN** no file is written

#### Scenario: A write failure is reported
- **WHEN** the user confirms the file-save dialog and writing the file fails (e.g. the path is not
  writable)
- **THEN** the system shows a modal error dialog whose message is the failure's error text

### Requirement: Import Templates
The system SHALL allow the user to import templates from a local JSON file, in the same shape
Export produces, via O while the Templates tab holds focus, choosing the file via a native
file-open dialog. Every entry in the file SHALL be validated before any write is made: on success,
each entry SHALL be written into the `lazynats-templates` bucket (creating it first if it did not
already exist), with an entry whose name matches an existing template overwriting it; the template
list SHALL then be refreshed. If any entry fails validation, the system SHALL write nothing and
report the failure.

#### Scenario: O opens a file-open dialog
- **WHEN** the user presses O while the Templates tab holds focus
- **THEN** a native file-open dialog opens, restricted to existing files

#### Scenario: Confirming a valid file imports every entry
- **WHEN** the user confirms the file-open dialog with a file containing entries that all pass
  validation
- **THEN** every entry is written into the `lazynats-templates` bucket (creating the bucket first
  if it did not already exist), and the template list is refreshed to include them

#### Scenario: An entry with a colliding name overwrites the existing template
- **WHEN** the imported file contains an entry whose name matches an existing template
- **THEN** the existing template's Subject, Headers, Payload Type, and Payload are overwritten
  with the imported entry's values

#### Scenario: An invalid entry blocks the entire import
- **WHEN** the imported file contains at least one entry that fails validation (e.g. an empty
  Subject, or a Payload that is invalid for its Payload Type)
- **THEN** the system writes nothing to the `lazynats-templates` bucket, and shows a modal error
  dialog identifying the failing entry

#### Scenario: A malformed file blocks the entire import
- **WHEN** the chosen file's contents are not valid JSON, or not shaped as a JSON object of
  template entries
- **THEN** the system writes nothing to the `lazynats-templates` bucket, and shows a modal error
  dialog reporting the failure

#### Scenario: Cancelling the dialog imports nothing
- **WHEN** the user opens the file-open dialog via O and cancels it instead of confirming
- **THEN** no templates are imported and the template list is unchanged
