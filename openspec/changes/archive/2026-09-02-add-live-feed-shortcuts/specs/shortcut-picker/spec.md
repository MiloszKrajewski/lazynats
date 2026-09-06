## MODIFIED Requirements

### Requirement: Picker Lists the Focused View's Advertised Shortcuts
The picker SHALL list the shortcuts aggregated (via focus-chain aggregation) for the view that
had focus when the picker was opened, ordered alphabetically by shortcut name rather than by key
chord. It SHALL NOT list the application's hardcoded top-level shortcuts (`Alt-1..5`, `Alt-M`,
`Alt-P`, `Alt-Q`, `?` itself) — those are already permanently visible in the status bar.

#### Scenario: List reflects the focus chain at open time
- **WHEN** the picker is opened while a particular view has keyboard focus
- **THEN** the listed shortcuts include every shortcut advertised by that view and its
  focus-chain ancestors, and none of the hardcoded top-level shortcuts

#### Scenario: Alphabetical ordering by name
- **WHEN** the picker is populated
- **THEN** entries are ordered alphabetically (case-insensitive) by shortcut name, not by key
  chord

#### Scenario: Focused view advertises no shortcuts
- **WHEN** the picker is opened while the focused view (and its ancestors) advertise no
  shortcuts at all
- **THEN** the picker shows an empty-state message instead of a blank list

### Requirement: Picker Suppresses Underlying Shortcut Keys While Open
While the picker is open, pressing a key combination that would normally trigger a shortcut in
the view underneath, or one of the application's hardcoded top-level shortcuts, SHALL NOT invoke
that shortcut; a per-view shortcut is only reachable by selecting it in the picker and pressing
Enter.

#### Scenario: Underlying shortcut key has no effect while picker is open
- **WHEN** the picker is open and the user presses a key combination bound to a shortcut in the
  view that was focused before the picker opened
- **THEN** that shortcut's action is not invoked as a side effect of the key press

#### Scenario: Top-level shortcut key has no effect while picker is open
- **WHEN** the picker is open and the user presses one of the application's hardcoded top-level
  shortcut keys (e.g. `Alt-1..5`, `Alt-M`, `Alt-P`, `Alt-Q`, or the picker's own `?` trigger)
- **THEN** that shortcut's action is not invoked, the picker remains open, and no second picker
  is opened
