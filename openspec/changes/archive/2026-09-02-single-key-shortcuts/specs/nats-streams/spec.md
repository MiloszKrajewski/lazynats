## MODIFIED Requirements

### Requirement: Manual List Refresh
The system SHALL NOT automatically refresh the stream list on a timer. The system SHALL allow the
user to refresh the stream list on demand via R, re-fetching the set of streams from the
server. If the previously-highlighted stream is still present in the refreshed list, it SHALL
remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the Streams tab is selected and a stream is created or deleted on the server via
  another client, without the user pressing R
- **THEN** the stream list shown in the app does not change

#### Scenario: R re-fetches the list
- **WHEN** the user presses R while the Streams tab holds focus
- **THEN** the stream list is re-fetched from the server and the displayed list reflects any
  streams created or deleted since the last fetch

#### Scenario: Refresh preserves the highlight when possible
- **WHEN** the user presses R and the previously-highlighted stream is still present in the
  refreshed list
- **THEN** that same stream remains highlighted after the refresh

#### Scenario: Refresh falls back to the first item when the highlight is gone
- **WHEN** the user presses R and the previously-highlighted stream is no longer present in
  the refreshed list
- **THEN** the first item in the refreshed list becomes highlighted, provided the list is
  non-empty

### Requirement: Stream List Filter
The system SHALL allow the user, while at the stream level, to set a filter pattern via F
that narrows the currently-loaded stream list to names matching that pattern, using the same
`* ? >` filter-expression grammar as every other list's F filter (see
`list-filter-affordance`). Since the stream list is always fetched in full (see "Stream List"),
this filter narrows only what is displayed, never what is fetched, and persists across an R
refresh until cleared or explicitly changed. This is independent of the existing in-memory
quick-search (`/`); both may be active at once.

#### Scenario: F opens the filter dialog
- **WHEN** the user presses F while the stream-level list holds focus
- **THEN** a modal dialog opens with an editable expression field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a valid pattern narrows the stream list
- **WHEN** the user enters a valid, non-empty pattern and confirms
- **THEN** only currently-loaded stream names matching that pattern remain shown

#### Scenario: An invalid pattern cannot be confirmed
- **WHEN** the entered pattern contains an empty token (a leading, trailing, or doubled `.`)
- **THEN** the dialog does not confirm on Enter

#### Scenario: Confirming an empty pattern clears the filter
- **WHEN** a filter is currently active, the user opens the dialog, clears the pattern field to
  empty, and confirms
- **THEN** the active filter is cleared and every currently-loaded stream is shown again

#### Scenario: The filter persists across a refresh
- **WHEN** a filter is active and the user presses R
- **THEN** the refreshed stream list is immediately narrowed by the still-active filter

### Requirement: Manual Consumer List Refresh
The system SHALL NOT automatically refresh the consumer list on a timer. The system SHALL allow
the user to refresh the consumer list on demand via R while at the consumer level,
re-fetching the set of consumers from the server. If the previously-highlighted consumer is still
present in the refreshed list, it SHALL remain highlighted; otherwise the first item in the
refreshed list SHALL become highlighted.

#### Scenario: The consumer list does not change on its own
- **WHEN** the consumer level is shown and a consumer is created or deleted on the server via
  another client, without the user pressing R
- **THEN** the consumer list shown in the app does not change

#### Scenario: R re-fetches the consumer list
- **WHEN** the user presses R while the consumer level holds focus
- **THEN** the consumer list is re-fetched from the server and the displayed list reflects any
  consumers created or deleted since the last fetch

### Requirement: Consumer List Filter
The system SHALL allow the user, while at the consumer level, to set a filter pattern via F
that narrows the currently-loaded consumer list (for the currently drilled-into stream) to names
matching that pattern, using the same `* ? >` filter-expression grammar as every other list's
F filter (see `list-filter-affordance`). Since the consumer list is always fetched in full
(see "Consumer List"), this filter narrows only what is displayed, never what is fetched. The
active filter SHALL reset when the user ascends back to the stream list, so a later descent into
any stream (including the same one) starts unfiltered — mirroring how the consumer list itself is
always re-fetched fresh on descend. This is independent of the existing in-memory quick-search
(`/`); both may be active at once.

#### Scenario: F opens the filter dialog
- **WHEN** the user presses F while the consumer-level list holds focus
- **THEN** a modal dialog opens with an editable expression field, seeded with the currently active
  filter pattern, or empty if no filter is active

#### Scenario: Confirming a valid pattern narrows the consumer list
- **WHEN** the user enters a valid, non-empty pattern and confirms
- **THEN** only currently-loaded consumer names matching that pattern remain shown

#### Scenario: The filter persists across a consumer-list refresh
- **WHEN** a filter is active and the user presses R while at the consumer level
- **THEN** the refreshed consumer list is immediately narrowed by the still-active filter

#### Scenario: The filter resets on ascend
- **WHEN** a filter is active at the consumer level and the user ascends (Esc/Backspace) back to
  the stream list
- **THEN** the filter is cleared, so a later descent into any stream's consumers starts unfiltered

### Requirement: Delete Consumer
The system SHALL allow the user to delete the highlighted consumer from the consumer-level list
via D. Before deleting, the system SHALL prompt the user to confirm, naming the consumer to
be deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the consumer on the server and refresh the
consumer list so the deleted consumer no longer appears.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the consumer-level list holds focus and a consumer is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted consumer

#### Scenario: Confirming deletes the consumer
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the consumer on the server, and the consumer list is refreshed so
  the deleted consumer no longer appears

#### Scenario: Highlight moves to a neighboring consumer after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the consumer that was immediately below the deleted consumer becomes highlighted, or
  the consumer immediately above it if the deleted consumer was last in the list, or no consumer
  is highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no consumer is deleted and the consumer list is unchanged

#### Scenario: Delete is only reachable at the consumer level
- **WHEN** the Streams tab is displayed at the stream level (not drilled into a stream)
- **THEN** D has no effect on any consumer, since none is displayed

### Requirement: Create Consumer
The system SHALL allow the user to create a new JetStream durable consumer on the currently
drilled-into stream from the consumer-level list via N, which opens a modal dialog collecting
Name, Filter Subjects, Ack Policy, and Deliver Policy. The target stream is not a dialog field — it
is always the stream the consumer list is currently showing. Every consumer created through this
dialog SHALL be durable; the dialog SHALL NOT provide a way to create an ephemeral consumer. On
confirmation the system SHALL create the consumer on the server with the entered values and
refresh the consumer list so the new consumer is shown and highlighted. The dialog SHALL only be
reachable from the consumer level; N SHALL have no effect at the stream level.

#### Scenario: N opens the create-consumer dialog
- **WHEN** the user presses N while the consumer-level list holds focus
- **THEN** a modal dialog opens with fields for Name, Filter Subjects, Ack Policy, and Deliver
  Policy, scoped to the currently drilled-into stream

#### Scenario: Confirming creates a durable consumer on the current stream
- **WHEN** the user fills in a valid Name, optionally fills in Filter Subjects, selects an Ack
  Policy and a Deliver Policy, and confirms (Create)
- **THEN** the system creates a durable consumer (with that Name as its durable name) on the
  currently drilled-into stream, the dialog closes, and the consumer list is refreshed with the
  new consumer shown and highlighted

#### Scenario: Filter Subjects field accepts multiple delimiters
- **WHEN** the user enters filter subjects separated by any mix of spaces, commas, or semicolons
  (e.g. `orders.new, orders.cancelled;  orders.shipped`)
- **THEN** the system parses each non-empty, delimiter-separated token as a distinct filter
  subject

#### Scenario: Filter subjects are always created via the consumer's multi-subject filter field
- **WHEN** one or more filter subjects are entered and the dialog is confirmed
- **THEN** the created consumer's filters are set via its multi-subject list field, never its
  singular filter-subject field, regardless of how many filter subjects were entered

#### Scenario: Empty Filter Subjects means no filter
- **WHEN** the user leaves the Filter Subjects field empty and confirms
- **THEN** the created consumer receives messages matching every subject on the stream, not just
  a filtered subset

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-consumer dialog and cancels (Esc) instead of confirming
- **THEN** no consumer is created and the consumer list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the create (e.g. duplicate durable
  name, invalid filter subject)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-consumer dialog reopens with the previously entered
  values still filled in

#### Scenario: N has no effect at the stream level
- **WHEN** the user presses N while the stream-level list holds focus
- **THEN** no create-consumer dialog opens (N at the stream level opens the create-stream
  dialog instead, per "Create Stream")

### Requirement: Edit Consumer
The system SHALL allow the user to edit the highlighted consumer from the consumer-level list via
E, which opens the same modal dialog used for "Create Consumer" in edit mode: the title and
confirm action read "Edit Consumer"/"Save", Name, Ack Policy, and Deliver Policy are shown but
disabled (all three are rejected by the server on update), and Filter Subjects remains editable.
On confirmation the system SHALL update the consumer on the server with the edited filter, always
writing it through the consumer's multi-subject filter field — clearing the singular
filter-subject field if the consumer previously had one set via that field instead — and
preserving every field the dialog does not expose (ack wait, max deliver, max ack pending,
description, metadata, ...) unchanged. The consumer list SHALL be refreshed so the updated
consumer's detail panel reflects the new filter.

#### Scenario: E opens the edit-consumer dialog
- **WHEN** the user presses E while the consumer-level list holds focus and a consumer is
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

#### Scenario: E has no effect at the stream level
- **WHEN** the user presses E while the stream-level list holds focus
- **THEN** no edit-consumer dialog opens (E at the stream level opens the edit-stream dialog
  instead, per "Edit Stream")

#### Scenario: E with no consumer highlighted does nothing
- **WHEN** the user presses E while the consumer-level list holds focus and the list is empty
  (no consumer highlighted)
- **THEN** no edit-consumer dialog opens

### Requirement: Create Stream
The system SHALL allow the user to create a new JetStream stream from the stream-level list via
N, which opens a modal dialog collecting Name, Subjects, Retention, and Max Age. On
confirmation the system SHALL create the stream on the server with the entered values and
refresh the stream list so the new stream is shown and highlighted. Fields not exposed in this
dialog (storage backend, message/byte/consumer limits, replica count, discard policy, ...) SHALL
be created with safe, explicit defaults rather than left unset, so the resulting stream is
immediately usable.

#### Scenario: N opens the create-stream dialog
- **WHEN** the user presses N while the stream-level list holds focus
- **THEN** a modal dialog opens with fields for Name, Subjects, Retention, and Max Age

#### Scenario: Confirming a valid dialog creates the stream
- **WHEN** the user fills in a valid Name and Subjects, selects a Retention policy, optionally
  sets a Max Age, and confirms (Create)
- **THEN** the system creates the stream on the server, the dialog closes, and the stream list is
  refreshed with the new stream shown and highlighted

#### Scenario: Cancelling the dialog creates nothing
- **WHEN** the user opens the create-stream dialog and cancels (Esc) instead of confirming
- **THEN** no stream is created and the stream list is unchanged

#### Scenario: Server-side create failure is reported without losing entered values
- **WHEN** the user confirms the dialog and the server rejects the create (e.g. duplicate name,
  invalid subject)
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  after the user dismisses it the create-stream dialog reopens with the previously entered
  values still filled in

#### Scenario: Unexposed limits default to unlimited, not zero
- **WHEN** a stream is created via this dialog without adjusting any field beyond Name, Subjects,
  Retention, and Max Age
- **THEN** the created stream has no message-count or byte-size limit (accepts messages
  indefinitely up to server-wide limits) and at least one replica, rather than a stream that
  rejects messages immediately

### Requirement: Edit Stream
The system SHALL allow the user to edit the highlighted stream from the stream-level list via
E, which opens the same modal dialog used for "Create Stream" in edit mode: the title and
confirm action read "Edit Stream"/"Save", Name and Retention are shown but disabled (both are
immutable on the server once the stream exists), and Subjects and Max Age remain editable. On
confirmation the system SHALL update the stream on the server with the edited Subjects/Max Age,
preserving every field the dialog does not expose (replica count, byte/message limits, discard
policy, description, metadata, ...) unchanged from the stream's current server-side configuration,
and refresh the stream list so the updated stream's detail panel reflects the new values.

#### Scenario: E opens the edit-stream dialog
- **WHEN** the user presses E while the stream-level list holds focus and a stream is
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

#### Scenario: E has no effect at the consumer level
- **WHEN** the user presses E while the consumer-level list holds focus
- **THEN** no edit-stream dialog opens (E at the consumer level opens the edit-consumer
  dialog instead, per "Edit Consumer")

#### Scenario: E with no stream highlighted does nothing
- **WHEN** the user presses E while the stream-level list holds focus and the list is empty
  (no stream highlighted)
- **THEN** no edit-stream dialog opens

### Requirement: Delete Stream
The system SHALL allow the user to delete the highlighted stream from the stream-level list via
D. Before deleting, the system SHALL prompt the user to confirm, naming the stream to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the stream on the server and refresh the stream
list so the deleted stream no longer appears.

#### Scenario: D prompts for confirmation
- **WHEN** the user presses D while the stream-level list holds focus and a stream is
  highlighted
- **THEN** a confirmation prompt opens, naming the highlighted stream

#### Scenario: Confirming deletes the stream
- **WHEN** the user confirms the deletion prompt (Delete)
- **THEN** the system deletes the stream on the server, and the stream list is refreshed so the
  deleted stream no longer appears

#### Scenario: Highlight moves to a neighboring stream after delete
- **WHEN** the user confirms deletion and the delete succeeds
- **THEN** the stream that was immediately below the deleted stream becomes highlighted, or the
  stream immediately above it if the deleted stream was last in the list, or no stream is
  highlighted if the list is now empty

#### Scenario: Cancelling the prompt deletes nothing
- **WHEN** the user dismisses the deletion prompt via Cancel or Esc instead of confirming
- **THEN** no stream is deleted and the stream list is unchanged

#### Scenario: Enter alone does not delete
- **WHEN** the deletion prompt is open and the user presses Enter without first moving focus to
  the Delete option
- **THEN** the prompt's default (Cancel) response is activated and no stream is deleted

#### Scenario: Server-side delete failure is reported
- **WHEN** the user confirms the deletion prompt and the server rejects the delete
- **THEN** the system shows a modal error dialog whose message is the failure's error text, and
  the stream list is unchanged

#### Scenario: D with no stream highlighted does nothing
- **WHEN** the user presses D while the stream-level list holds focus and the list is empty
  (no stream highlighted)
- **THEN** no confirmation prompt opens and nothing is deleted
