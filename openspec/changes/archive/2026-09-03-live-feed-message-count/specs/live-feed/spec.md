## ADDED Requirements

### Requirement: Live Feed Status on Border
The system SHALL render the live feed's current buffered count and follow/sticky state as status
text on the "Live Feed" host frame's bottom border row, right corner, owned and drawn by the host
container (not by the live feed view itself, per the "No In-View Header" requirement), and SHALL
keep it in sync with the feed's buffer as messages arrive, are evicted, or the feed is cleared, and
with the feed's follow/pause state as it changes.

While the feed is following (auto-scrolled to the newest message), the status text SHALL show the
total buffered count followed by a downward-arrow glyph, padded with a leading and trailing space,
and the entire status text SHALL be colored green. While the feed is sticky (paused - the
selection has stopped tracking the newest message), the status text SHALL instead show the
selected message's 1-based position out of the total buffered count, followed by a filled-circle
glyph, padded with a leading and trailing space, and the entire status text SHALL be colored
yellow.

#### Scenario: Count reflects arriving messages while following
- **WHEN** a new message arrives in the live feed and is added to its buffer, and the feed is
  following
- **THEN** the status text on the "Live Feed" frame's border shows the buffer's new total count
  followed by the downward-arrow glyph

#### Scenario: Count reflects eviction at the buffer cap
- **WHEN** the buffer exceeds its maximum size and the oldest messages are evicted to bring it
  back to the cap
- **THEN** the status text reflects the buffer's size after eviction, not before

#### Scenario: Status resets on Clear
- **WHEN** the user clears the live feed
- **THEN** the status text updates to show a total count of zero

#### Scenario: Status switches to sticky when the user moves the selection
- **WHEN** the user moves the selection away from the newest message while the feed is following
- **THEN** the feed becomes sticky, and the status text switches to showing the selected message's
  1-based position out of the total buffered count, followed by the filled-circle glyph

#### Scenario: Sticky position stays current as more messages arrive
- **WHEN** the feed is sticky and additional messages arrive, changing the total buffered count
- **THEN** the status text's total updates to match, while the selected message's position is
  preserved unless that message itself is evicted

#### Scenario: Status switches back to following when the user resumes following
- **WHEN** the user resumes following (e.g. via the follow/pause toggle) while the feed is sticky
- **THEN** the status text switches back to showing the total buffered count followed by the
  downward-arrow glyph

#### Scenario: Status survives resize and focus changes
- **WHEN** the terminal is resized, or keyboard focus moves into or out of the live feed
- **THEN** the status text remains visible and correctly painted on the border, undisturbed by the
  border's own redraw

#### Scenario: Status text is colored green while following
- **WHEN** the status text is shown while following
- **THEN** the entire status text (count, padding, and the downward-arrow glyph) is rendered in
  green

#### Scenario: Status text is colored yellow while sticky
- **WHEN** the status text is shown while sticky
- **THEN** the entire status text (position/count, padding, and the filled-circle glyph) is
  rendered in yellow
