## ADDED Requirements

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
  until a subsequent `Show` call or poll tick changes them

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
