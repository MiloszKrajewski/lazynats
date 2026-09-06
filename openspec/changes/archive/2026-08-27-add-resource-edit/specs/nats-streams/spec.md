## REMOVED Requirements

### Requirement: Read-Only List
**Reason**: Superseded by "Edit Stream" and "Edit Consumer" below — both levels now provide an
edit affordance alongside create/delete, so the "no edit affordance" guarantee this requirement
made no longer holds.
**Migration**: None. This is a UI capability change with no stored data or API contract to
migrate; the previous restriction simply no longer applies.

## ADDED Requirements

### Requirement: Edit Stream
The system SHALL allow the user to edit the highlighted stream from the stream-level list via
Ctrl+E, which opens the same modal dialog used for "Create Stream" in edit mode: the title and
confirm action read "Edit Stream"/"Save", Name and Retention are shown but disabled (both are
immutable on the server once the stream exists), and Subjects and Max Age remain editable. On
confirmation the system SHALL update the stream on the server with the edited Subjects/Max Age,
preserving every field the dialog does not expose (replica count, byte/message limits, discard
policy, description, metadata, ...) unchanged from the stream's current server-side configuration,
and refresh the stream list so the updated stream's detail panel reflects the new values.

#### Scenario: Ctrl+E opens the edit-stream dialog
- **WHEN** the user presses Ctrl+E while the stream-level list holds focus and a stream is
  highlighted
- **THEN** a modal dialog opens, seeded with that stream's current Name, Subjects, Retention, and
  Max Age, with its title and confirm button reading "Edit"/"Save"

#### Scenario: Name and Retention are locked
- **WHEN** the edit-stream dialog is open
- **THEN** the Name and Retention fields show the stream's current values but cannot be changed

#### Scenario: Confirming updates Subjects and Max Age without touching other server-side config
- **WHEN** the user changes Subjects and/or Max Age and confirms (Save)
- **THEN** the system updates only those fields on the server, the dialog closes, and the stream
  list is refreshed with the updated stream highlighted, its other server-side configuration
  (replica count, limits, discard policy, description, metadata, ...) unchanged

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-stream dialog and cancels (Esc) instead of confirming
- **THEN** the stream is not updated and the stream list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the update
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-stream dialog reopens with the previously entered values
  still filled in

#### Scenario: Ctrl+E has no effect at the consumer level
- **WHEN** the user presses Ctrl+E while the consumer-level list holds focus
- **THEN** no edit-stream dialog opens (Ctrl+E at the consumer level opens the edit-consumer
  dialog instead, per "Edit Consumer")

#### Scenario: Ctrl+E with no stream highlighted does nothing
- **WHEN** the user presses Ctrl+E while the stream-level list holds focus and the list is empty
  (no stream highlighted)
- **THEN** no edit-stream dialog opens

### Requirement: Edit Stream Field Validation
The edit-stream dialog SHALL apply the same Subjects and Max Age validation as "Create Stream
Field Validation" to the fields it leaves editable. Name and Retention, being disabled, are exempt
from validation — their value is unchanged from the stream's current server-side configuration.

#### Scenario: Empty subjects blocks saving
- **WHEN** the Subjects field is empty, or contains only delimiter characters with no actual
  subject text
- **THEN** the Save action is unavailable and the Subjects field is flagged invalid

#### Scenario: Unparseable Max Age blocks saving
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Save action is unavailable and the Max Age field is flagged invalid

#### Scenario: Empty Max Age means unlimited
- **WHEN** the Max Age field is cleared and the rest of the dialog is otherwise valid
- **THEN** the Save action is available and saving removes the stream's maximum message age

### Requirement: Edit Consumer
The system SHALL allow the user to edit the highlighted consumer from the consumer-level list via
Ctrl+E, which opens the same modal dialog used for "Create Consumer" in edit mode: the title and
confirm action read "Edit Consumer"/"Save", Name, Ack Policy, and Deliver Policy are shown but
disabled (all three are rejected by the server on update), and Filter Subjects remains editable.
On confirmation the system SHALL update the consumer on the server with the edited filter, always
writing it through the consumer's multi-subject filter field — clearing the singular
filter-subject field if the consumer previously had one set via that field instead — and
preserving every field the dialog does not expose (ack wait, max deliver, max ack pending,
description, metadata, ...) unchanged. The consumer list SHALL be refreshed so the updated
consumer's detail panel reflects the new filter.

#### Scenario: Ctrl+E opens the edit-consumer dialog
- **WHEN** the user presses Ctrl+E while the consumer-level list holds focus and a consumer is
  highlighted
- **THEN** a modal dialog opens, seeded with that consumer's current Name, Filter Subjects, Ack
  Policy, and Deliver Policy, scoped to the currently drilled-into stream, with its title and
  confirm button reading "Edit"/"Save"

#### Scenario: Name, Ack Policy, and Deliver Policy are locked
- **WHEN** the edit-consumer dialog is open
- **THEN** the Name, Ack Policy, and Deliver Policy fields show the consumer's current values but
  cannot be changed

#### Scenario: Confirming updates Filter Subjects without touching other server-side config
- **WHEN** the user changes Filter Subjects and confirms (Save)
- **THEN** the system updates only the filter on the server, the dialog closes, and the consumer
  list is refreshed with the updated consumer highlighted, its other server-side configuration
  (ack wait, max deliver, max ack pending, description, metadata, ...) unchanged

#### Scenario: Editing clears a previously-set singular filter field
- **WHEN** the highlighted consumer's filter was previously set via its singular filter-subject
  field (e.g. by a client other than lazynats) and the user confirms an edit
- **THEN** the updated consumer's filter is held entirely in the multi-subject filter field, with
  the singular filter-subject field cleared

#### Scenario: Cancelling the dialog changes nothing
- **WHEN** the user opens the edit-consumer dialog and cancels (Esc) instead of confirming
- **THEN** the consumer is not updated and the consumer list is unchanged

#### Scenario: Server-side update failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the update
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the edit-consumer dialog reopens with the previously entered values
  still filled in

#### Scenario: Ctrl+E has no effect at the stream level
- **WHEN** the user presses Ctrl+E while the stream-level list holds focus
- **THEN** no edit-consumer dialog opens (Ctrl+E at the stream level opens the edit-stream dialog
  instead, per "Edit Stream")

#### Scenario: Ctrl+E with no consumer highlighted does nothing
- **WHEN** the user presses Ctrl+E while the consumer-level list holds focus and the list is empty
  (no consumer highlighted)
- **THEN** no edit-consumer dialog opens

### Requirement: Edit Consumer Field Validation
The edit-consumer dialog SHALL treat Filter Subjects as always valid, the same as when creating
(empty is a valid "no filter" choice, not an error). Name, Ack Policy, and Deliver Policy, being
disabled, are exempt from validation.

#### Scenario: Empty Filter Subjects on save means no filter
- **WHEN** the user clears Filter Subjects entirely and confirms (Save)
- **THEN** the Save action is available, and after saving the consumer receives messages matching
  every subject on the stream, not just a filtered subset
