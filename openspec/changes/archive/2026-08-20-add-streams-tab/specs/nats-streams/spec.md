## ADDED Requirements

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

### Requirement: Read-Only List
The Streams tab list SHALL NOT provide any means to create, edit, or delete a stream.

#### Scenario: No mutation affordance is present
- **WHEN** the Streams tab is displayed
- **THEN** no keybinding, button, or other control for creating, editing, or deleting a stream is
  present
