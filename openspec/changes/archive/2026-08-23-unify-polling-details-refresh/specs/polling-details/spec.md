## MODIFIED Requirements

### Requirement: SetTarget Re-Points Without Fetching
A polling details panel SHALL expose an operation to change which target its poll pipeline
refetches, and this operation SHALL NOT itself trigger a fetch or alter currently displayed rows.

#### Scenario: Changing the target does not immediately fetch
- **WHEN** the panel's target is changed
- **THEN** no fetch occurs as a direct result of that change, and previously displayed rows remain
  until a subsequent `Show` call, the debounced target-change fetch (per "Immediate Fetch On
  Demand"), or a poll tick changes them

### Requirement: Immediate Fetch On Demand
A polling details panel SHALL automatically fetch the current target shortly after every target
change, independent of the active-gate and the poll interval — for every subclass, not only one
whose paired list has no cached data to `Show()` synchronously on a highlight change. Successive
target changes within a brief settling window SHALL be debounced so that only the target still
current once the window elapses is fetched. Clearing the target (no target set) SHALL cancel any
pending or in-flight fetch immediately, without itself displaying anything — clearing the visible
content remains the caller's own `Show`-based responsibility, unaffected by this requirement. A
result that arrives for a target the panel has since moved on from SHALL NOT be applied.

#### Scenario: A target change is fetched without waiting for the next poll tick
- **WHEN** the panel's target changes and the settling window elapses without a further change
- **THEN** the system fetches that target and, once the fetch completes successfully, shows the
  result — without waiting for the poll interval to elapse

#### Scenario: Rapid successive target changes fetch only the final target
- **WHEN** the target changes more than once within the settling window
- **THEN** only the target that is still current once the window elapses is fetched; no fetch
  occurs for a target that was superseded before the window elapsed

#### Scenario: No fetch and no display change when the target is cleared
- **WHEN** the target is cleared (no target set)
- **THEN** no fetch occurs as a result of the clear, and the panel's displayed content is
  unaffected unless the caller separately calls `Show` with no value

#### Scenario: Clearing the target cancels a pending or in-flight fetch
- **WHEN** the target is cleared while an earlier fetch for the previous target is queued in the
  settling window or already in flight
- **THEN** that fetch's result, once it arrives, is not shown

#### Scenario: A stale result from either trigger is not applied
- **WHEN** a fetch is in flight — whether triggered by a target change or by a poll tick — and the
  target changes again before that fetch completes
- **THEN** the now-stale result, once it arrives, is not shown
