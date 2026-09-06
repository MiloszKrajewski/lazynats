# nats-templates Specification

## Purpose
Provide a fifth management tab, `5:Templates`, for saving and reusing NATS message templates
(Subject, Headers, Payload Type, Payload) so the user doesn't have to retype common messages when
publishing. Templates are stored server-side in a dedicated KV bucket (`lazynats-templates`) so
they persist across sessions and are shared with other `lazynats` clients against the same server.

## Requirements
### Requirement: Templates Tab
The system SHALL provide a fifth management tab, titled `5:Templates` and reachable from anywhere
via Alt+5, presenting a single flat list of message templates (no drill-down levels) alongside a
detail panel for the currently highlighted template, the same list/details split every other
management tab already uses.

#### Scenario: Alt+5 selects the Templates tab
- **WHEN** the user presses Alt+5 from anywhere in the application
- **THEN** the Templates tab becomes the selected management tab

### Requirement: Template Detail Panel
The system SHALL show, alongside the template list, a detail panel for the currently highlighted
template, presenting its Name, Subject, and Payload Type, followed by a blank line, its Headers
(one `key: value` line per header), a further blank line, and finally its Payload text.

#### Scenario: Highlighting a template shows its details
- **WHEN** the user moves the highlight to a template in the list
- **THEN** the detail panel shows that template's Name, Subject, Payload Type, Headers, and
  Payload, in that fixed layout

#### Scenario: No template highlighted
- **WHEN** the template list is empty and no template is highlighted
- **THEN** the detail panel shows no template's details

#### Scenario: A template with no headers still shows the blank line before Payload
- **WHEN** the highlighted template has no headers
- **THEN** the detail panel's layout is unchanged (Name/Subject/Payload Type, a blank line, no
  header lines, a further blank line, then Payload) rather than collapsing the empty Headers
  section away

### Requirement: Template Storage
The system SHALL store each template as a JSON document in a dedicated NATS KV bucket named
`lazynats-templates`, one entry per template, keyed by the template's Name. The document SHALL
capture the template's Subject, Headers, Payload Type, and Payload; Name SHALL NOT be duplicated
inside the document since it is already the KV key.

#### Scenario: A saved template is stored as a KV entry
- **WHEN** a template named `get-invoice` with Subject `invoices.get` is created
- **THEN** the `lazynats-templates` bucket contains an entry keyed `get-invoice` whose value is a
  JSON document carrying that template's Subject, Headers, Payload Type, and Payload

### Requirement: Template List
The system SHALL list the names of all templates currently present in the `lazynats-templates`
bucket, fetched when the Templates tab is first activated (given keyboard focus).

#### Scenario: Existing templates are listed
- **WHEN** the Templates tab is activated and one or more templates exist in the bucket
- **THEN** the list shows each template's name

#### Scenario: No templates exist
- **WHEN** the Templates tab is activated and the `lazynats-templates` bucket either contains no
  entries or does not exist yet
- **THEN** the list shows the same non-interactive empty-state hint in both cases, with no visible
  distinction between "bucket exists but is empty" and "bucket does not exist yet"

### Requirement: Bucket Is Never Created On Read
The system SHALL NOT create the `lazynats-templates` bucket as a side effect of listing or
refreshing templates. The bucket SHALL only come into existence as a side effect of a successful
Create or Edit (see "Create Template" and "Edit Template").

#### Scenario: Viewing an empty Templates tab creates nothing
- **WHEN** the `lazynats-templates` bucket does not exist and the user activates or refreshes the
  Templates tab without creating a template
- **THEN** the bucket still does not exist afterward

### Requirement: Manual Template List Refresh
The system SHALL NOT automatically refresh the template list on a timer. The system SHALL allow
the user to refresh it on demand via Ctrl+R, re-fetching the set of templates from the bucket. If
the previously-highlighted template is still present in the refreshed list, it SHALL remain
highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the Templates tab is selected and a template is added, edited, or removed in the bucket
  via another client, without the user pressing Ctrl+R
- **THEN** the template list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the list
- **WHEN** the user presses Ctrl+R while the Templates list holds focus
- **THEN** the list is re-fetched from the `lazynats-templates` bucket and reflects any templates
  added or removed since the last fetch

### Requirement: Create Template
The system SHALL allow the user to create a new template via Ctrl+N, opening a modal dialog
collecting Name, Subject, Headers (key/value pairs), Payload Type (`Json`, `Text`, or `Base64`,
defaulting to `Text`), and Payload (multi-line text). On confirmation the system SHALL ensure the
`lazynats-templates` bucket exists (creating it with safe defaults if it does not), write the new
entry, and refresh the template list so the new template is shown and highlighted.

#### Scenario: Ctrl+N opens the create-template dialog
- **WHEN** the user presses Ctrl+N while the Templates list holds focus
- **THEN** a modal dialog opens with empty Name, Subject, Headers, and Payload fields, and Payload
  Type defaulted to `Text`

#### Scenario: The Headers field has no filter/search of its own
- **WHEN** the create-template (or edit-template) dialog is open
- **THEN** its Headers field offers no quick-search ("/") or filter (Ctrl+F) of its own, and is at
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

### Requirement: Create Template Field Validation
The create-template dialog SHALL validate Name, Subject, and Payload before allowing confirmation,
and SHALL visually flag an invalid field rather than allowing a request that will fail or produce
an unusable template.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty subject blocks creation
- **WHEN** the Subject field is empty
- **THEN** the Create action is unavailable and the Subject field is flagged invalid

#### Scenario: Invalid JSON blocks creation when Payload Type is Json
- **WHEN** Payload Type is `Json` and the Payload field's text does not parse as valid JSON
- **THEN** the Create action is unavailable and the Payload field is flagged invalid

#### Scenario: Invalid Base64 blocks creation when Payload Type is Base64
- **WHEN** Payload Type is `Base64` and the Payload field's text does not decode as valid base64
- **THEN** the Create action is unavailable and the Payload field is flagged invalid

#### Scenario: Any text is valid when Payload Type is Text
- **WHEN** Payload Type is `Text`
- **THEN** the Payload field's text is never flagged invalid, including when it is empty

#### Scenario: Switching Payload Type re-validates the current Payload text
- **WHEN** the Payload field already contains text that is invalid for the newly selected Payload
  Type (e.g. non-JSON text left over after switching from `Text` to `Json`)
- **THEN** the Create action becomes unavailable and the Payload field is flagged invalid without
  requiring the user to re-type the Payload text

### Requirement: Edit Template
The system SHALL allow the user to edit the highlighted template via Ctrl+E, opening the same
modal dialog used for "Create Template" in edit mode: the title and confirm action read "Edit
Template"/"Save", Name is shown but disabled (immutable once the template exists, since it is the
KV key), and Subject, Headers, Payload Type, and Payload are seeded with the template's current
values and remain editable. On confirmation the system SHALL overwrite the entry in the
`lazynats-templates` bucket and refresh the template list so the updated template's row reflects
any changed content.

#### Scenario: Ctrl+E opens the edit-template dialog
- **WHEN** the user presses Ctrl+E while the Templates list holds focus and a template is
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

#### Scenario: Ctrl+E with no template highlighted does nothing
- **WHEN** the user presses Ctrl+E while the Templates list holds focus and the list is empty (no
  template highlighted)
- **THEN** no edit-template dialog opens

### Requirement: Edit Template Field Validation
The edit-template dialog SHALL apply the same Subject and Payload validation as "Create Template
Field Validation" to the fields it leaves editable. Name, being disabled, is exempt.

#### Scenario: Empty subject blocks saving
- **WHEN** the Subject field is empty
- **THEN** the Save action is unavailable and the Subject field is flagged invalid

#### Scenario: Invalid Payload for the selected Payload Type blocks saving
- **WHEN** the Payload field's text is invalid for the currently selected Payload Type (per
  "Create Template Field Validation")
- **THEN** the Save action is unavailable and the Payload field is flagged invalid

### Requirement: Delete Template
The system SHALL allow the user to delete the highlighted template via Ctrl+D. Before deleting,
the system SHALL prompt the user to confirm, naming the template to be deleted, with the
non-destructive choice (Cancel) as the prompt's default (Enter-activated) response. On
confirmation the system SHALL delete the entry from the `lazynats-templates` bucket and refresh
the template list so the deleted template no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the Templates list holds focus and a template is
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
The system SHALL offer the same shared, in-memory quick-search (`/`) and filter (Ctrl+F,
`* ? >` expression grammar per `list-filter-affordance`) wiring `drillable-list` already specifies,
applied to template names, with both narrowing only what is displayed - never what is fetched or
which template Ctrl+E/Ctrl+D act on.

#### Scenario: Quick-search narrows the displayed templates
- **WHEN** the user presses `/` and types a query matching some, but not all, template names as a
  case-insensitive subsequence
- **THEN** only the matching templates remain shown

#### Scenario: Filter narrows the displayed templates
- **WHEN** the user presses Ctrl+F, enters a valid, non-empty pattern, and confirms
- **THEN** only template names matching that pattern remain shown

#### Scenario: Edit and Delete act on the correct template regardless of filtering
- **WHEN** quick-search or the filter narrows the displayed templates, the user selects one of the
  displayed templates, and presses Ctrl+E or Ctrl+D
- **THEN** the operation acts on that same selected template, not a different one
