## ADDED Requirements

### Requirement: Tab Cycles Within the Selected Tab's Content
The system SHALL, when keyboard focus is within a tab's content, keep Tab and Shift+Tab focus
movement within that same tab's content: focus SHALL cycle among the content's own focusable
views and SHALL NOT pass through the tab header or leave the tab. Every focusable view in the
content SHALL remain reachable by repeated Tab presses, regardless of which of them was focused
most recently. This SHALL hold for every management tab by default, without a tab having to opt
in. On a tab whose content has a single focusable view, Tab SHALL leave focus where it is. The
header SHALL remain reachable from content via Up, per "Up Climbs From Content to the Current
Tab's Header".

#### Scenario: Tab cycles through all of a tab's focusable views
- **WHEN** a tab's content has two or more focusable views (e.g. the Subscribe tab's subscription
  list and exclusion filter input), focus is on one of them, and the user presses Tab repeatedly
- **THEN** focus visits each of them in turn and returns to the first, without ever landing on the
  tab header

#### Scenario: A previously visited view stays reachable
- **WHEN** the user has moved focus from the Subscribe tab's list to its exclusion filter input
  via Tab
- **THEN** a further Tab (while the Apply button is disabled) returns focus to the list

#### Scenario: Tab on a single-stop tab stays put
- **WHEN** focus is on the Streams tab's list, which is that tab's only focusable view, and the
  user presses Tab
- **THEN** focus remains on the list and the tab header is not focused

#### Scenario: The header remains reachable via Up
- **WHEN** focus has been cycled with Tab within a tab's content and the user then presses Up at
  the top of the content
- **THEN** focus moves to that tab's own header
