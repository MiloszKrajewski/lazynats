# shortcut-picker Specification

## Purpose
TBD - created by archiving change add-shortcut-picker. Update Purpose after archive.
## Requirements
### Requirement: Global Shortcut Picker Invocation
The system SHALL provide a global `?` shortcut, available regardless of which management tab
or the live feed has focus, that opens a modal shortcut picker dialog.

#### Scenario: Opening the picker from any tab
- **WHEN** the user presses `?` while any management tab or the live feed has focus
- **THEN** a modal shortcut picker dialog opens

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

### Requirement: Selecting an Entry Runs It
Selecting an entry and pressing Enter SHALL close the picker and then invoke that entry's
action, in that order.

#### Scenario: Enter runs the selected shortcut
- **WHEN** the user highlights an entry in the picker and presses Enter
- **THEN** the picker closes, and only after it has closed is the entry's action invoked

### Requirement: Escape Cancels Without Action
Pressing Esc while the picker is open SHALL close it without invoking any shortcut's action.

#### Scenario: Esc closes without running anything
- **WHEN** the user presses Esc while the picker is open
- **THEN** the picker closes and no shortcut action is invoked

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

### Requirement: Entry Rows Are Presented Key-First and Colored
Each listed entry SHALL be rendered as its key chord followed by its action name, reversing the
name-then-key order of a plain listing. The key portion SHALL be colored distinctly from the
action name, and right-padded to a column width computed from the longest key chord present in
the picker's current entry set, so that no key chord is ever truncated and the action-name column
starts at the same position on every row.

#### Scenario: Key precedes the action name
- **WHEN** the picker lists an entry for a shortcut bound to a given key and name
- **THEN** the rendered row shows the key chord first, followed by the action name

#### Scenario: Key column width fits the widest key currently listed
- **WHEN** the picker's current entry set includes a key chord longer than the picker's minimum
  column width
- **THEN** the key column is wide enough to show that key chord in full, uncut, on its own row
  and aligned with every other row's key column

#### Scenario: Key text is visually distinct from the action name
- **WHEN** a row is rendered
- **THEN** the key portion is drawn in a distinct color and the action-name portion is drawn
  without that color, composing with (not overriding) the row's normal or selected highlight
