## ADDED Requirements

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
A polling details panel SHALL expose an operation that fetches the current target immediately,
independent of both the active-gate and the poll interval, for a subclass whose paired list has
no cached data to `Show()` synchronously on a highlight change. This operation SHALL have no
effect when no target is set, and a result that arrives after the target has since changed SHALL
NOT be applied.

#### Scenario: Immediate fetch shows the result without waiting for the next poll tick
- **WHEN** the immediate-fetch operation is invoked while a target is set
- **THEN** the system fetches that target right away and, once the fetch completes
  successfully, shows the result — without waiting for the poll interval to elapse

#### Scenario: No effect with no target set
- **WHEN** the immediate-fetch operation is invoked while no target is set
- **THEN** no fetch occurs

#### Scenario: A stale result is not applied
- **WHEN** the immediate-fetch operation is invoked, and before it completes the target changes
  again (e.g. via a subsequent highlight change)
- **THEN** the now-stale result, once it arrives, is not shown
