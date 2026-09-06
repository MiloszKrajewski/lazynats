## MODIFIED Requirements

### Requirement: Read-Only List
The Streams tab's consumer level SHALL NOT provide any means to edit or delete a consumer. The
stream level SHALL NOT provide any means to edit a stream (creating and deleting a stream are
both provided — see "Create Stream" and "Delete Stream"; creating a consumer is also provided —
see "Create Consumer").

#### Scenario: No edit affordance is present at the stream level
- **WHEN** the Streams tab is displayed at the stream level
- **THEN** no keybinding, button, or other control for editing a stream is present

#### Scenario: No edit or delete affordance is present at the consumer level
- **WHEN** the Streams tab is displayed at the consumer level (drilled into a stream)
- **THEN** no keybinding, button, or other control for editing or deleting a consumer is present

## ADDED Requirements

### Requirement: Create Consumer
The system SHALL allow the user to create a new JetStream durable consumer on the currently
drilled-into stream from the consumer-level list via Ctrl+N, which opens a modal dialog collecting
Name, Filter Subjects, Ack Policy, and Deliver Policy. The target stream is not a dialog field — it
is always the stream the consumer list is currently showing. Every consumer created through this
dialog SHALL be durable; the dialog SHALL NOT provide a way to create an ephemeral consumer. On
confirmation the system SHALL create the consumer on the server with the entered values and
refresh the consumer list so the new consumer is shown and highlighted. The dialog SHALL only be
reachable from the consumer level; Ctrl+N SHALL have no effect at the stream level.

#### Scenario: Ctrl+N opens the create-consumer dialog
- **WHEN** the user presses Ctrl+N while the consumer-level list holds focus
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

#### Scenario: Ctrl+N has no effect at the stream level
- **WHEN** the user presses Ctrl+N while the stream-level list holds focus
- **THEN** no create-consumer dialog opens (Ctrl+N at the stream level opens the create-stream
  dialog instead, per "Create Stream")

### Requirement: Create Consumer Field Validation
The create-consumer dialog SHALL validate Name before allowing confirmation, and SHALL visually
flag an invalid Name rather than allowing a request that will fail immediately. Filter Subjects is
optional (empty, or containing only delimiter characters with no actual subject text, is a valid,
meaningful choice), and Ack Policy and Deliver Policy always have a valid default selection.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Ack Policy always has a valid selection
- **WHEN** the create-consumer dialog is opened
- **THEN** the Ack Policy field already shows a default selection (Explicit) and never blocks
  Create on its own

#### Scenario: Ack Policy offers only user-facing choices
- **WHEN** the user opens the Ack Policy field
- **THEN** the offered choices are limited to Explicit, All, and None — FlowControl (used
  internally by durable consumers driving mirror or source replication) is not offered

#### Scenario: Deliver Policy always has a valid selection
- **WHEN** the create-consumer dialog is opened
- **THEN** the Deliver Policy field already shows a default selection (All) and never blocks
  Create on its own

#### Scenario: Deliver Policy offers only start-position-independent choices
- **WHEN** the user opens the Deliver Policy field
- **THEN** the offered choices are limited to All, Last, New, and Last Per Subject — choices that
  require a companion start position (By Start Sequence, By Start Time) are not offered
