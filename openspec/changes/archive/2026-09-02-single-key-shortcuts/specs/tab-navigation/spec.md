## MODIFIED Requirements

### Requirement: Up Climbs From Content to the Current Tab's Header
The system SHALL, when keyboard focus is within a tab's content and an Up key press is left
unhandled by that content, move keyboard focus to that same tab's own header, without changing
which tab is selected.

#### Scenario: Up at the top of a tab's content focuses that tab's own header
- **WHEN** keyboard focus is within the Subscribe tab's content, an Up key press is left unhandled
  by everything in that content (e.g. it reaches the top of the list)
- **THEN** keyboard focus moves to the Subscribe tab's own header, and the Subscribe tab remains
  the selected tab

#### Scenario: Up does not jump to a different tab's header
- **WHEN** keyboard focus climbs from a tab's content to that tab's own header via Up
- **THEN** focus lands on the header of the tab whose content it climbed from, not on any other
  tab's header
