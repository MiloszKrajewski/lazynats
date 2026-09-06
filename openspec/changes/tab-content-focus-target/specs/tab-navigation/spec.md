## MODIFIED Requirements

### Requirement: Down Returns Focus From a Header to Its Tab's Content
The system SHALL, when keyboard focus is on a tab's header, move keyboard focus back into that
tab's content in response to Down. That focus SHALL land on an interactive descendant view within
the content (e.g. a list, a text field) capable of visibly indicating that it holds focus, never on
a non-interactive container view that merely hosts other content, even when such a container is
itself capable of receiving focus.

#### Scenario: Down from a focused header re-enters the tab's content
- **WHEN** keyboard focus is on the Subscribe tab's header and the user presses Down
- **THEN** keyboard focus moves into the Subscribe tab's content

#### Scenario: Down never lands on a non-interactive container within the content
- **WHEN** keyboard focus is on a tab's header, the user presses Down, and that tab's content root
  is itself capable of receiving focus while also containing its own focusable descendant views
- **THEN** keyboard focus lands on one of those descendant views, not on the content root itself
