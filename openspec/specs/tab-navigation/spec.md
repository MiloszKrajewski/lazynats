# tab-navigation Specification

## Purpose
TBD - created by syncing change tab-navigation-and-shortcuts. Update Purpose after archive.

## Requirements

### Requirement: Tab Titles
The system SHALL title the live-monitoring management tab "Subscribe" and the message-composition
management tab "Publish", presenting them as a matched verb pair.

#### Scenario: Monitoring tab is titled Subscribe
- **WHEN** the management tab area is displayed
- **THEN** the tab previously titled "Subscriptions" is titled "Subscribe"

### Requirement: Alt+Letter Tab Switching
The system SHALL allow the user to switch directly to a management tab from anywhere in the
application via a dedicated Alt+letter shortcut, unique per tab: Alt+B for Subscribe, Alt+P for
Publish.

#### Scenario: Alt+B switches to Subscribe from anywhere
- **WHEN** the user presses Alt+B while focus is anywhere in the application, including inside
  another tab's content
- **THEN** the Subscribe tab becomes the selected tab

#### Scenario: Alt+P switches to Publish from anywhere
- **WHEN** the user presses Alt+P while focus is anywhere in the application, including inside
  another tab's content
- **THEN** the Publish tab becomes the selected tab

### Requirement: Up Climbs From Content to the Current Tab's Header
The system SHALL, when keyboard focus is within a tab's content and an Up key press is left
unhandled by that content, move keyboard focus to that same tab's own header, without changing
which tab is selected.

#### Scenario: Up at the top of a tab's content focuses that tab's own header
- **WHEN** keyboard focus is within the Subscribe tab's content, an Up key press is left unhandled
  by everything in that content (e.g. it reaches the top of the list and then the text input above
  it), and the user presses Up again
- **THEN** keyboard focus moves to the Subscribe tab's own header, and the Subscribe tab remains
  the selected tab

#### Scenario: Up does not jump to a different tab's header
- **WHEN** keyboard focus climbs from a tab's content to that tab's own header via Up
- **THEN** focus lands on the header of the tab whose content it climbed from, not on any other
  tab's header

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

### Requirement: Tab Switching via Arrows Requires Header Focus
The system SHALL change the selected management tab in response to Left/Right only while keyboard
focus is on a tab's header, and SHALL NOT change the selected tab in response to Left/Right (or
any other arrow key) while focus is within a tab's content.

#### Scenario: Left/Right switch tabs while a header is focused
- **WHEN** keyboard focus is on the Subscribe tab's header and the user presses Right
- **THEN** the selected management tab changes and keyboard focus moves to the newly selected
  tab's header

#### Scenario: Left/Right within a tab's content do not change tabs
- **WHEN** keyboard focus is on a view within a tab's content that does not itself act on Left or
  Right, and the user presses Left or Right
- **THEN** the selected management tab is unchanged and focus does not move to any header

#### Scenario: Up within a tab's content never jumps directly to a different tab
- **WHEN** keyboard focus is within a tab's content and the user presses Up (whether or not it is
  left unhandled by that content)
- **THEN** the selected management tab is unchanged
