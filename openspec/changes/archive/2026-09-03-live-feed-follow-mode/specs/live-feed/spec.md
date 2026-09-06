## ADDED Requirements

### Requirement: Identity-Preserving Selection Across Rollover
When the ring buffer evicts messages from the front to stay within its cap, the system SHALL
shift a selection that is not on the evicted row down by one row per eviction, so a selected
message remains selected - at whatever row it has shifted to - until it is itself the message
evicted. Once the selected message is the one evicted, the system SHALL leave selection on row 0,
landing on whatever message is newly the oldest surviving message after that eviction; no other
message remains to track.

#### Scenario: Selection follows the same message through partial rollover
- **WHEN** the user has selected a message that is not the newest, and enough new messages arrive
  to evict messages older than the selected one but not the selected one itself
- **THEN** the same message remains selected afterward, at whatever row index it has shifted to

#### Scenario: Selection lands on the new oldest message once its own message is evicted
- **WHEN** the user's selected message is itself the next one evicted, because it was the oldest
  surviving message at the moment the buffer next exceeds its cap
- **THEN** selection lands on whatever message is newly at row 0 after that eviction

### Requirement: Clear Resets Selection
The system SHALL clear the list selection whenever `Clear` empties the message list, so no
highlight is left rendered at a stale row index once the list is empty.

#### Scenario: Selection is cleared along with the message list
- **WHEN** the user presses `Clear` (`C`) while a message is selected
- **THEN** the message list becomes empty and no row remains highlighted

### Requirement: Explicit Follow/Pause State
The system SHALL track whether the live feed is following (moving the selection to each newly
arrived message) as an explicit state, not inferred from whether the current selection happens to
occupy the last row. Manual navigation SHALL always pause following, regardless of which row it
lands on, including the last row. `Space` SHALL be the sole way to resume following: pressing it
while paused SHALL move the selection to the newest currently-buffered message and resume
following; pressing it while already following SHALL pause without moving the selection. While
following, each newly arrived message SHALL move the selection to it. While paused, newly arrived
messages SHALL NOT move the selection.

#### Scenario: Manual navigation pauses following even when it lands on the newest message
- **WHEN** the feed is following and the user navigates (e.g. presses Down or End) such that the
  selection lands on the newest message
- **THEN** following is paused, and a subsequently arriving message does not move the selection

#### Scenario: Space pauses in place while following
- **WHEN** the feed is following and the user presses `Space`
- **THEN** following is paused and the current selection does not move

#### Scenario: Space resumes and jumps to the newest message while paused
- **WHEN** the feed is paused and the user presses `Space`
- **THEN** following resumes and the selection moves to the newest currently-buffered message

#### Scenario: New messages move the selection while following
- **WHEN** the feed is following and a new message arrives
- **THEN** the selection moves to that newly arrived message

#### Scenario: New messages do not move the selection while paused
- **WHEN** the feed is paused and a new message arrives
- **THEN** the selection does not move

### Requirement: Vertical Scrollbar on Feed List
The live feed's message list SHALL show a vertical scrollbar reflecting the selection's position
within the currently buffered messages, so the user has a visual sense of position within the
buffer while paused and browsing.

#### Scenario: Scrollbar reflects position while paused and browsing
- **WHEN** the feed is paused and the user has selected a message partway through the buffered
  messages
- **THEN** a vertical scrollbar is visible on the feed list, indicating that position
