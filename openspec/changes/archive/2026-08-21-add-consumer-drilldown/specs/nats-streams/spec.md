## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Read-Only List
The Streams tab lists, at either the stream level or the consumer level, SHALL NOT provide any
means to create, edit, or delete a stream or consumer.

#### Scenario: No mutation affordance is present at the stream level
- **WHEN** the Streams tab is displayed at the stream level
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a stream is
  present

#### Scenario: No mutation affordance is present at the consumer level
- **WHEN** the Streams tab is displayed at the consumer level (drilled into a stream)
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a consumer
  is present
