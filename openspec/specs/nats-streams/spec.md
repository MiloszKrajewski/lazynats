# nats-streams Specification

## Purpose
Provide a "Streams" management tab that lists JetStream streams on the connected server, lets
the user create new streams, and shows configuration/state detail for the highlighted stream,
and lets the user drill down from a stream into its consumers to inspect their configuration and
delivery/ack state (read-only at the consumer level), so the user can manage and inspect
JetStream state without leaving the terminal.

## Requirements

### Requirement: Stream List
The system SHALL provide a Streams management tab listing the names of all JetStream streams
currently present on the connected server.

#### Scenario: Existing streams are listed
- **WHEN** one or more JetStream streams exist on the server
- **THEN** the Streams tab's list shows each stream's name

#### Scenario: No streams exist
- **WHEN** no JetStream streams exist on the server (or JetStream is not enabled)
- **THEN** the Streams tab shows a non-interactive hint in place of the list, rather than a blank
  list

### Requirement: Stream Detail Panel
The system SHALL show, alongside the stream list, a detail panel for the currently highlighted
stream, presenting at least its configuration (subjects, retention policy, limits, replica count)
and current state (message count, byte size, first/last sequence number, consumer count).

#### Scenario: Highlighting a stream shows its details
- **WHEN** the user moves the highlight to a stream in the list
- **THEN** the detail panel shows that stream's configuration and current state

#### Scenario: No stream highlighted
- **WHEN** the stream list is empty and no stream is highlighted
- **THEN** the detail panel shows no stream's details

### Requirement: Periodic Detail Refresh
The system SHALL refresh the highlighted stream's detail panel periodically while the Streams tab
is the selected tab, independent of any selection change, so that state changes made outside the
application (e.g. via another NATS client) become visible without user interaction. This
refresh SHALL apply only to the detail panel, not to the stream list.

#### Scenario: Detail panel reflects a change made outside the app
- **WHEN** the Streams tab is selected, a stream is highlighted, and that stream's message count
  changes on the server (e.g. a message is published to it) without any selection change in the
  app
- **THEN** the detail panel's shown message count updates within one refresh cycle

#### Scenario: Refresh does not run while the tab is not selected
- **WHEN** the Streams tab is not the currently selected management tab
- **THEN** the system does not poll the server for detail updates

### Requirement: Manual List Refresh
The system SHALL NOT automatically refresh the stream list on a timer. The system SHALL allow the
user to refresh the stream list on demand via Ctrl+R, re-fetching the set of streams from the
server. If the previously-highlighted stream is still present in the refreshed list, it SHALL
remain highlighted; otherwise the first item in the refreshed list SHALL become highlighted.

#### Scenario: The list does not change on its own
- **WHEN** the Streams tab is selected and a stream is created or deleted on the server via
  another client, without the user pressing Ctrl+R
- **THEN** the stream list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the list
- **WHEN** the user presses Ctrl+R while the Streams tab holds focus
- **THEN** the stream list is re-fetched from the server and the displayed list reflects any
  streams created or deleted since the last fetch

#### Scenario: Refresh preserves the highlight when possible
- **WHEN** the user presses Ctrl+R and the previously-highlighted stream is still present in the
  refreshed list
- **THEN** that same stream remains highlighted after the refresh

#### Scenario: Refresh falls back to the first item when the highlight is gone
- **WHEN** the user presses Ctrl+R and the previously-highlighted stream is no longer present in
  the refreshed list
- **THEN** the first item in the refreshed list becomes highlighted, provided the list is
  non-empty

### Requirement: Navigation Between Stream and Consumer Levels
The system SHALL allow the user to descend from the stream list into the highlighted stream's
consumer list, and climb back up to the stream list, with the current level always visually
obvious.

#### Scenario: Enter descends into a stream's consumers
- **WHEN** the user presses Enter while a stream is highlighted in the stream list
- **THEN** the LHS list is replaced with that stream's consumer list, and the RHS switches to
  tracking the highlighted consumer

#### Scenario: Esc climbs back to the stream list
- **WHEN** the user presses Esc while viewing a stream's consumer list
- **THEN** the LHS list is replaced with the stream list, restored to its prior highlight and
  scroll position, and the RHS switches back to tracking the highlighted stream

#### Scenario: Backspace climbs back to the stream list
- **WHEN** the user presses Backspace while viewing a stream's consumer list
- **THEN** the same result as pressing Esc occurs

#### Scenario: Current level is shown in the breadcrumb
- **WHEN** the user has descended into a stream's consumers
- **THEN** the LHS and RHS panel titles reflect the consumer level (e.g. naming the stream) rather
  than the generic stream-level titles

#### Scenario: Descending with no consumers
- **WHEN** the user descends into a stream that has no consumers
- **THEN** the consumer list shows a non-interactive hint in place of the list, rather than a
  blank list

### Requirement: Consumer List
The system SHALL list the names of all JetStream consumers belonging to the currently
drilled-into stream, fetched fresh every time the user descends into that level.

#### Scenario: Descending fetches the consumer list
- **WHEN** the user descends into a stream via Enter
- **THEN** the system fetches the current set of consumers for that stream from the server and
  displays them

#### Scenario: Re-descending re-fetches
- **WHEN** the user ascends from a stream's consumer list and then descends into the same stream
  again
- **THEN** the system fetches the consumer list again rather than reusing the previous result

#### Scenario: Ascending does not re-fetch the stream list
- **WHEN** the user ascends from a stream's consumer list back to the stream list
- **THEN** the stream list is not re-fetched from the server; it shows whatever it last held

### Requirement: Consumer Detail Panel
The system SHALL show, alongside the consumer list, a detail panel for the currently highlighted
consumer, presenting at least its configuration (filter subject, ack policy, deliver policy, max
deliver, max ack pending) and current state (delivered sequence, ack floor, ack-pending count,
redelivered count, waiting count, pending count).

#### Scenario: Highlighting a consumer shows its details
- **WHEN** the user moves the highlight to a consumer in the consumer list
- **THEN** the detail panel shows that consumer's configuration and current state

#### Scenario: No consumer highlighted
- **WHEN** the consumer list is empty and no consumer is highlighted
- **THEN** the detail panel shows no consumer's details

### Requirement: Periodic Consumer Detail Refresh
The system SHALL refresh the highlighted consumer's detail panel periodically while the consumer
level is shown and the Streams tab is the selected tab, independent of any selection change. This
refresh SHALL apply only to the detail panel, not to the consumer list.

#### Scenario: Detail panel reflects a change made outside the app
- **WHEN** the consumer level is shown, a consumer is highlighted, and that consumer's ack-pending
  count changes on the server without any selection change in the app
- **THEN** the detail panel's shown ack-pending count updates within one refresh cycle

#### Scenario: Refresh does not run while the stream level is shown
- **WHEN** the user is viewing the stream list (not drilled into a stream's consumers)
- **THEN** the system does not poll the server for consumer detail updates

#### Scenario: Refresh does not run while the tab is not selected
- **WHEN** the Streams tab is not the currently selected management tab
- **THEN** the system does not poll the server for consumer detail updates, even if the consumer
  level was the last one shown

### Requirement: Manual Consumer List Refresh
The system SHALL NOT automatically refresh the consumer list on a timer. The system SHALL allow
the user to refresh the consumer list on demand via Ctrl+R while at the consumer level,
re-fetching the set of consumers from the server. If the previously-highlighted consumer is still
present in the refreshed list, it SHALL remain highlighted; otherwise the first item in the
refreshed list SHALL become highlighted.

#### Scenario: The consumer list does not change on its own
- **WHEN** the consumer level is shown and a consumer is created or deleted on the server via
  another client, without the user pressing Ctrl+R
- **THEN** the consumer list shown in the app does not change

#### Scenario: Ctrl+R re-fetches the consumer list
- **WHEN** the user presses Ctrl+R while the consumer level holds focus
- **THEN** the consumer list is re-fetched from the server and the displayed list reflects any
  consumers created or deleted since the last fetch

### Requirement: Read-Only List
The Streams tab's consumer level SHALL NOT provide any means to edit a consumer (deleting a
consumer is provided — see "Delete Consumer"). The stream level SHALL NOT provide any means to
edit a stream (creating and deleting a stream are both provided — see "Create Stream" and "Delete
Stream"; creating a consumer is also provided — see "Create Consumer").

#### Scenario: No edit affordance is present at the stream level
- **WHEN** the Streams tab is displayed at the stream level
- **THEN** no keybinding, button, or other control for editing a stream is present

#### Scenario: No edit affordance is present at the consumer level
- **WHEN** the Streams tab is displayed at the consumer level (drilled into a stream)
- **THEN** no keybinding, button, or other control for editing a consumer is present

### Requirement: Delete Consumer
The system SHALL allow the user to delete the highlighted consumer from the consumer-level list
via Ctrl+D. Before deleting, the system SHALL prompt the user to confirm, naming the consumer to
be deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the consumer on the server and refresh the
consumer list so the deleted consumer no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the consumer-level list holds focus and a consumer is
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
- **THEN** Ctrl+D has no effect on any consumer, since none is displayed

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

### Requirement: Create Stream
The system SHALL allow the user to create a new JetStream stream from the stream-level list via
Ctrl+N, which opens a modal dialog collecting Name, Subjects, Retention, and Max Age. On
confirmation the system SHALL create the stream on the server with the entered values and
refresh the stream list so the new stream is shown and highlighted. Fields not exposed in this
dialog (storage backend, message/byte/consumer limits, replica count, discard policy, ...) SHALL
be created with safe, explicit defaults rather than left unset, so the resulting stream is
immediately usable.

#### Scenario: Ctrl+N opens the create-stream dialog
- **WHEN** the user presses Ctrl+N while the stream-level list holds focus
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

### Requirement: Create Stream Field Validation
The create-stream dialog SHALL validate Name, Subjects, and Max Age before allowing
confirmation, and SHALL visually flag invalid fields rather than allowing a request that will
fail immediately.

#### Scenario: Empty name blocks creation
- **WHEN** the Name field is empty or whitespace-only
- **THEN** the Create action is unavailable and the Name field is flagged invalid

#### Scenario: Empty subjects blocks creation
- **WHEN** the Subjects field is empty, or contains only delimiter characters with no actual
  subject text
- **THEN** the Create action is unavailable and the Subjects field is flagged invalid

#### Scenario: Subjects field accepts multiple delimiters
- **WHEN** the user enters subjects separated by any mix of spaces, commas, or semicolons (e.g.
  `orders.*, orders.new;  orders.cancelled`)
- **THEN** the system parses each non-empty, delimiter-separated token as a distinct subject

#### Scenario: Empty Max Age means unlimited
- **WHEN** the Max Age field is left empty and the rest of the dialog is otherwise valid
- **THEN** the Create action is available and the created stream has no maximum message age

#### Scenario: Unparseable Max Age blocks creation
- **WHEN** the Max Age field contains text that does not parse as a `TimeSpan`
- **THEN** the Create action is unavailable and the Max Age field is flagged invalid

#### Scenario: Retention always has a valid selection
- **WHEN** the create-stream dialog is opened
- **THEN** the Retention field already shows a default selection (Limits) and never blocks
  Create on its own

### Requirement: Delete Stream
The system SHALL allow the user to delete the highlighted stream from the stream-level list via
Ctrl+D. Before deleting, the system SHALL prompt the user to confirm, naming the stream to be
deleted, with the non-destructive choice (Cancel) as the prompt's default (Enter-activated)
response. On confirmation the system SHALL delete the stream on the server and refresh the stream
list so the deleted stream no longer appears.

#### Scenario: Ctrl+D prompts for confirmation
- **WHEN** the user presses Ctrl+D while the stream-level list holds focus and a stream is
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

#### Scenario: Ctrl+D with no stream highlighted does nothing
- **WHEN** the user presses Ctrl+D while the stream-level list holds focus and the list is empty
  (no stream highlighted)
- **THEN** no confirmation prompt opens and nothing is deleted
