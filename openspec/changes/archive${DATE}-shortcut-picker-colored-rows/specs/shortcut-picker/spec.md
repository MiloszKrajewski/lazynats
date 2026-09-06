## ADDED Requirements

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
