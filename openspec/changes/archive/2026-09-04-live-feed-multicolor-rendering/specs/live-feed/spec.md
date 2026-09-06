## ADDED Requirements

### Requirement: Row Header Text Is Colored
A live feed row SHALL render its header segment (the message's headers, formatted as
space-separated `key=value` pairs) in the app's shared header color (`Theme.HeaderColor`),
distinct from the color used for the rest of that row, so headers are visually scannable while
skimming the feed. This SHALL hold regardless of the row's horizontal scroll position, and SHALL
be preserved — on top of whatever background the row already has — while the row is
selected/highlighted, rather than being flattened back to a single uniform color by selection.

#### Scenario: Headers render in the shared header color
- **WHEN** a live feed row for a message with one or more headers is rendered
- **THEN** the characters making up the header segment are drawn using `Theme.HeaderColor`, while
  the timestamp, subject, and payload segments keep their own current colors

#### Scenario: Header coloring survives horizontal scroll
- **WHEN** the live feed list is scrolled horizontally such that only part of a row's header
  segment is within the visible column window
- **THEN** the visible part of the header segment is still drawn in `Theme.HeaderColor`, and the
  portions of the row before and after it keep their current color

#### Scenario: Headers stay colored on a selected row
- **WHEN** a live feed row containing headers is the currently selected/highlighted row
- **THEN** the header segment is still drawn in `Theme.HeaderColor`, composed with the selected
  row's own background, rather than reverting to the row's non-header color

#### Scenario: A message with no headers has no header segment to color
- **WHEN** a live feed row is rendered for a message with no headers
- **THEN** no characters of that row are drawn in `Theme.HeaderColor`, and no error occurs

### Requirement: Row Payload Type Prefix Is Colored
A live feed row SHALL render its payload's type prefix (`(json) `, `(text) `, `(blob) `, or the
empty-payload `(empty)` form) in the app's shared payload-type color (`Theme.PayloadTypeColor`),
distinct from the color used for the payload body and the rest of that row, so the type indicator
is visually scannable while skimming the feed. This SHALL hold regardless of the row's horizontal
scroll position, and SHALL be preserved — on top of whatever background the row already has —
while the row is selected/highlighted, rather than being flattened back to a single uniform color
by selection.

#### Scenario: Payload type prefix renders in the shared payload-type color
- **WHEN** a live feed row is rendered
- **THEN** the characters making up the payload's type prefix are drawn using
  `Theme.PayloadTypeColor`, while the payload body and the rest of the row keep their own current
  colors

#### Scenario: The empty-payload indicator is colored as the type prefix
- **WHEN** a live feed row is rendered for a message whose payload renders as `(empty)` (an empty
  `Utf8Text`-classified payload)
- **THEN** the entire `(empty)` text is drawn using `Theme.PayloadTypeColor`

#### Scenario: Payload type prefix coloring survives horizontal scroll
- **WHEN** the live feed list is scrolled horizontally such that only part of a row's payload type
  prefix is within the visible column window
- **THEN** the visible part of the prefix is still drawn in `Theme.PayloadTypeColor`, and the
  portions of the row before and after it keep their current color

#### Scenario: Payload type prefix stays colored on a selected row
- **WHEN** a live feed row is the currently selected/highlighted row
- **THEN** the payload type prefix segment is still drawn in `Theme.PayloadTypeColor`, composed
  with the selected row's own background, rather than reverting to the row's non-prefix color

### Requirement: Row Omits Redundant Header Spacing When Absent
A live feed row SHALL NOT reserve the header segment's surrounding spacing when the message has no
headers; the row text SHALL place the payload directly after the subject with the same single
column gap used elsewhere between segments, rather than leaving a visible extra gap where an empty
header segment would otherwise sit.

#### Scenario: A message with no headers has no extra gap before the payload
- **WHEN** a live feed row is rendered for a message with no headers
- **THEN** the row text has exactly one column gap between the subject and the payload text, not
  the wider gap that would result from an empty header segment still being surrounded by its usual
  spacing

#### Scenario: A message with headers keeps the header segment's spacing
- **WHEN** a live feed row is rendered for a message with one or more headers
- **THEN** the row text places the header segment between subject and payload, each separated by
  the usual column gap, unchanged from today's spacing
