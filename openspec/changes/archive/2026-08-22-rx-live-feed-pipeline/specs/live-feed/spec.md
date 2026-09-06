## MODIFIED Requirements

### Requirement: Batched Main-Thread Dispatch
The system SHALL buffer messages from the shared feed for a fixed 25ms window - matching the
app's own render cadence closely enough to add no perceptible latency while avoiding
clock-phase-drift noise against it - and SHALL dispatch each non-empty window's messages to the
UI thread with a single call, so that UI-thread marshaling overhead does not scale with
per-message throughput.

#### Scenario: A burst of messages is dispatched as one batch
- **WHEN** multiple messages are published to the shared feed within the same 25ms window
- **THEN** all of them are dispatched together and applied to the feed view via a single
  UI-thread call, not one dispatch per message

#### Scenario: A window with no messages produces no dispatch
- **WHEN** a 25ms window elapses with no messages published to the shared feed
- **THEN** no UI-thread dispatch call occurs for that window

#### Scenario: Windows with pending dispatches are all applied on the next main-loop iteration
- **WHEN** the UI thread is busy long enough for more than one window's dispatch to become queued
- **THEN** all queued dispatches are applied on the next main-loop iteration before it redraws,
  none are dropped, and none wait for a further iteration
