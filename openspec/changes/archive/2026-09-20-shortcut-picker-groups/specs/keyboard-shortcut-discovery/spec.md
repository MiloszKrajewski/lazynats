## ADDED Requirements

### Requirement: Shortcuts Belong to a Group
Every advertised shortcut SHALL belong to a group. A shortcut source MAY assign a shortcut's
group explicitly; when it does not, the system SHALL default that shortcut's group to the
shortcut source that advertised it.

#### Scenario: A shortcut with no explicit group defaults to its source
- **WHEN** a shortcut source advertises a shortcut without assigning it a group
- **THEN** that shortcut's group is the shortcut source instance that advertised it

#### Scenario: A single source splits its shortcuts into more than one group
- **WHEN** a shortcut source advertises multiple shortcuts and explicitly assigns different
  groups to different subsets of them
- **THEN** each subset is treated as belonging to its own distinct group, separate from the
  source's default group

### Requirement: Presentation Order Keeps Groups Contiguous
The system SHALL present the currently available shortcut set with all shortcuts sharing a group
appearing contiguously, and groups appearing in the order their first shortcut was encountered
during aggregation. Grouping SHALL NOT be visually indicated (no separator, header, or group
name shown) - it governs ordering only.

#### Scenario: A source's declared order is preserved within its group
- **WHEN** a shortcut source advertises multiple shortcuts in a specific order and none of them
  declare an explicit priority
- **THEN** those shortcuts appear in the presented set in that same relative order

#### Scenario: Groups are not visually separated
- **WHEN** the shortcut picker presents shortcuts from more than one group
- **THEN** no separator, header, or group name is rendered between them

### Requirement: Priority Reorders Within a Group Only
A shortcut MAY declare a priority. When present, shortcuts within the same group SHALL be
ordered by ascending priority ahead of same-group shortcuts without an explicit priority, using
each shortcut's original relative position as the tiebreaker. A priority SHALL NOT move a
shortcut into a different group's span.

#### Scenario: An explicit priority moves a shortcut earlier within its group
- **WHEN** a shortcut source advertises several shortcuts in one group and assigns an explicit
  priority to one of them
- **THEN** that shortcut is presented ahead of the other same-group shortcuts that declared no
  priority, while their own relative order is otherwise unchanged

#### Scenario: Priority cannot cross a group boundary
- **WHEN** a shortcut in one group declares a priority
- **THEN** it is never presented interleaved with, or ahead of, shortcuts belonging to a
  different group
