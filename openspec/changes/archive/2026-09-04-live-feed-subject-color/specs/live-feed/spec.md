## ADDED Requirements

### Requirement: Row Subject Text Is Colored
A live feed row SHALL render its subject segment in the app's shared subject color
(`Theme.SubjectColor`), distinct from the color used for the rest of that row (timestamp,
headers, payload), so the subject is visually scannable while skimming the feed. This SHALL hold
regardless of the row's horizontal scroll position, and SHALL be preserved — on top of whatever
background the row already has — while the row is selected/highlighted, rather than being
flattened back to a single uniform color by selection.

#### Scenario: Subject renders in the shared subject color
- **WHEN** a live feed row is rendered
- **THEN** the characters making up that message's subject are drawn using
  `Theme.SubjectColor`, while the timestamp, headers, and payload segments are drawn using their
  current (unchanged) color

#### Scenario: Subject coloring survives horizontal scroll
- **WHEN** the live feed list is scrolled horizontally such that only part of a row's subject is
  within the visible column window
- **THEN** the visible part of the subject is still drawn in `Theme.SubjectColor`, and the
  portions of the row before and after it keep their current color

#### Scenario: Subject stays colored when its row is out of view
- **WHEN** the live feed list is scrolled horizontally such that a row's subject falls entirely
  outside the visible column window
- **THEN** the row renders with no visible characters in `Theme.SubjectColor` for that row (there
  is nothing of the subject on screen to color), and no error occurs

#### Scenario: Subject stays colored on a selected row
- **WHEN** a live feed row containing a subject is the currently selected/highlighted row
- **THEN** the subject segment is still drawn in `Theme.SubjectColor`, composed with the
  selected row's own background, rather than reverting to the row's non-subject color
