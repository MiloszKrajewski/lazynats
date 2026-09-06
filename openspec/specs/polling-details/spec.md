# polling-details Specification

## Purpose
TBD - created by archiving change extract-drillable-list-base. Update Purpose after archive.
## Requirements
### Requirement: Label:Value Detail Rendering
A polling details panel SHALL render its current content as right-aligned labels in a shared
column followed by their values, built by an overridable row-building function from the current
target's fetched data. A row with an empty label SHALL be treated as a blank separator line rather
than a labeled row.

#### Scenario: Rows render as aligned label:value pairs
- **WHEN** a polling details panel has rows built from fetched data
- **THEN** each row renders with its label right-aligned to a shared column width, followed by its
  value

#### Scenario: An empty-label row renders as a blank separator
- **WHEN** a built row has an empty label
- **THEN** that row renders as a blank line rather than a labeled value

### Requirement: Show and Clear
A polling details panel SHALL expose an operation to display a given fetched value's rows, and
SHALL clear its displayed content (no rows) when given no value.

#### Scenario: Showing a value displays its rows
- **WHEN** the panel is shown a fetched value
- **THEN** the rows built from that value are displayed

#### Scenario: Showing no value clears the panel
- **WHEN** the panel is shown no value (e.g. because nothing is highlighted in a paired list)
- **THEN** no rows are displayed

### Requirement: SetTarget Re-Points Without Fetching
A polling details panel SHALL expose an operation to change which target its poll pipeline
refetches, and this operation SHALL NOT itself trigger a fetch or alter currently displayed rows.

#### Scenario: Changing the target does not immediately fetch
- **WHEN** the panel's target is changed
- **THEN** no fetch occurs as a direct result of that change, and previously displayed rows remain
  until a subsequent `Show` call, the debounced target-change fetch (per "Immediate Fetch On
  Demand"), or a poll tick changes them

### Requirement: Active-Gated Lazy Poll Pipeline
A polling details panel SHALL start its poll pipeline lazily, no earlier than the first time it is
marked active, and SHALL NOT restart that pipeline on subsequent activation changes. While marked
inactive, no fetch SHALL occur even if the poll interval elapses.

#### Scenario: The pipeline starts on first activation
- **WHEN** the panel is marked active for the first time
- **THEN** its poll pipeline begins running from that point on

#### Scenario: Deactivating stops fetches without tearing down the pipeline
- **WHEN** the panel is marked inactive after having been active
- **THEN** no further fetches occur while inactive, and reactivating later resumes fetches without
  the pipeline being recreated

### Requirement: Overridable Poll Interval
A polling details panel SHALL use a poll interval that defaults to 3 seconds and that a subclass
MAY override to a different value.

#### Scenario: A subclass overrides the poll interval
- **WHEN** a subclass overrides the poll interval to a non-default value
- **THEN** the panel's fetches occur at that overridden interval instead of the 3-second default

### Requirement: Polling Can Be Disabled Entirely
A polling details panel SHALL expose an overridable setting that, when disabled, prevents any
fetch from ever occurring, regardless of active state or elapsed time, while still supporting
`Show`/`SetTarget` and rendering rows built from data supplied some other way (e.g. a single
value shown once and never refetched).

#### Scenario: A subclass with polling disabled never fetches
- **WHEN** a subclass overrides polling to be disabled and is marked active with a target set
- **THEN** no fetch ever occurs for that panel, at any elapsed interval

#### Scenario: A polling-disabled subclass still renders shown data
- **WHEN** a subclass with polling disabled is shown a value directly
- **THEN** the rows built from that value are displayed, unaffected by polling being disabled

### Requirement: Fetch Failures Are Reported, Not Thrown
A polling details panel SHALL report a failed fetch through its own error-reporting path rather
than letting the failure propagate and terminate the poll pipeline, so the pipeline continues
running on its next interval after a failure.

#### Scenario: A failed fetch is reported and polling continues
- **WHEN** an active polling details panel's fetch fails
- **THEN** the failure is reported through the panel's error-reporting path, the previously
  displayed rows are left unchanged, and the next poll interval still results in a fetch attempt

### Requirement: Optional Body Region
A polling details panel SHALL support an overridable body-building function that, given the
current target's fetched data, produces an optional block of free-form text to render below the
label:value header rows, filling the panel's remaining height. A subclass that does not override
this function SHALL render exactly as if the body did not exist (header rows only, unchanged from
current behavior).

#### Scenario: A subclass with no body override renders rows only
- **WHEN** a subclass does not override the body-building function
- **THEN** the panel renders only its label:value header rows, identical to a panel with no body
  concept at all

#### Scenario: A subclass with a body override renders it below the header rows
- **WHEN** a subclass overrides the body-building function and it returns non-empty text for the
  current target's fetched data
- **THEN** that text is rendered starting below the header rows, filling the remaining panel
  height

#### Scenario: A body taller than the remaining space is clipped
- **WHEN** the body text produced for the current target would require more vertical space than
  remains below the header rows
- **THEN** the panel shows as much of the body as fits and does not scroll to reveal the rest

#### Scenario: Showing no value clears the body along with the rows
- **WHEN** the panel is shown no value (per the existing "Show and Clear" requirement)
- **THEN** no body text is displayed, in addition to no header rows being displayed

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
