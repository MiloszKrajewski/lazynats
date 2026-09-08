# live-feed Specification

## Purpose
Provide a single global, deduplicated, low-overhead live feed of messages merged across all active NATS subscriptions, independent of which subscription produced a given message.

## Requirements

### Requirement: Message Envelope
The system SHALL wrap each received `NatsMsg<byte[]>` in a `FeedEnvelope` carrying the client-side receipt timestamp and the identity of the subscription that produced it, before the message enters the shared feed pipeline, so that receipt time and provenance are available to every downstream consumer (dedup, rendering) without being recomputed or inferred later.

#### Scenario: Every message entering the feed pipeline carries receipt time and subscription identity
- **WHEN** a message is received from an active subscription
- **THEN** it is wrapped in a `FeedEnvelope` with a receipt timestamp and that subscription's identity before being written to the shared channel

### Requirement: Global Merged Feed Channel
The system SHALL merge messages from all active subscriptions into a single shared `Channel<FeedEnvelope>`, with one background reader task per active subscription writing into it, so the feed is a single global stream independent of how many subscriptions are active or which one produced a given message.

#### Scenario: Messages from multiple active subscriptions share one feed
- **WHEN** two different subject-pattern subscriptions are both active and each receives a message
- **THEN** both messages appear in the same global feed, in the order they were written to the shared channel

#### Scenario: Feed is not scoped to a selected subscription
- **WHEN** the user selects a specific subscription in the Subscriptions screen
- **THEN** the feed continues to show messages from all active subscriptions, not only the selected one

### Requirement: Batched Main-Thread Dispatch
The system SHALL buffer messages from the shared feed for a fixed 25ms window - matching the
app's own render cadence closely enough to add no perceptible latency while avoiding
clock-phase-drift noise against it - and SHALL dispatch each non-empty window's messages to the
UI thread with a single call, so that UI-thread marshaling overhead does not scale with
per-message throughput.

#### Scenario: A burst of messages is dispatched as one batch
- **WHEN** multiple messages are published to the shared feed within the same 25ms window
- **THEN** all of them are dispatched together and applied to the feed view via a single
  UI-thread call, not one dispatch per message

#### Scenario: A window with no messages produces no dispatch
- **WHEN** a 25ms window elapses with no messages published to the shared feed
- **THEN** no UI-thread dispatch call occurs for that window

#### Scenario: Windows with pending dispatches are all applied on the next main-loop iteration
- **WHEN** the UI thread is busy long enough for more than one window's dispatch to become queued
- **THEN** all queued dispatches are applied on the next main-loop iteration before it redraws,
  none are dropped, and none wait for a further iteration

### Requirement: Duplicate Collapsing for Overlapping Subscriptions
The system SHALL compute a dedup key as a 64-bit hash of `subject + headers + payload` from
each envelope's inner `NatsMsg<byte[]>`, deliberately excluding the envelope's receipt
timestamp and subscription identity from the hash itself. For each key, the system SHALL track
the receipt timestamp and `SubscriptionId` of the most recent envelope that was **not**
suppressed as a duplicate for that key (its "canonical" envelope). An incoming envelope SHALL be
suppressed as a duplicate only if a canonical envelope for the same key was recorded within a
short trailing time window and the incoming envelope's `SubscriptionId` differs from the
canonical one's. The system SHALL update the stored canonical timestamp and `SubscriptionId`
only when an envelope is **not** suppressed; a suppressed (duplicate) envelope SHALL leave the
stored canonical state unchanged. This distinguishes a message matching more than one active
overlapping subscription pattern (collapsed to one row) from a genuinely distinct repeat publish
of identical content (not collapsed), including when the two cases occur close together while
the same overlapping subscriptions remain active.

#### Scenario: The same message delivered via two overlapping subscriptions appears once
- **WHEN** subscriptions for `invoices.>` and `invoices.get.*` are both active and a message is published on `invoices.get.123`, causing the server to deliver it once per matching subscription
- **THEN** the feed shows exactly one row for that message

#### Scenario: Receipt timestamp is not part of the dedup key
- **WHEN** the same message is received twice (once per matching subscription) with different `FeedEnvelope.ReceivedAt` values
- **THEN** both receipts still produce the same dedup key and are collapsed to one row

#### Scenario: Subscription identity is excluded from the hash key but used to decide collapsing
- **WHEN** the same message is received via two different active subscriptions, each tagging its envelope with a different `SubscriptionId`
- **THEN** both envelopes still produce the same dedup key, and the second envelope is suppressed because its `SubscriptionId` differs from the first (canonical) envelope's

#### Scenario: A repeat publish on the same subscription is not collapsed
- **WHEN** a subscription's `SubscriptionId` receives two envelopes with identical subject, headers, and payload within the trailing dedup window
- **THEN** both envelopes appear as separate rows in the feed, because the second envelope's `SubscriptionId` matches the stored canonical `SubscriptionId` for that key

#### Scenario: Suppressed duplicates do not change which subscription is canonical
- **WHEN** a key's canonical envelope came from subscription A, and envelopes from other overlapping subscriptions (e.g. B, then C) are subsequently suppressed as duplicates of it
- **THEN** the stored canonical `SubscriptionId` for that key remains A after each suppression, so a later genuine repeat delivered again via subscription A is still recognized as matching the canonical subscription and is not collapsed

#### Scenario: Distinct messages outside the window are not collapsed
- **WHEN** two messages with identical subject, headers, and payload are received with a gap larger than the trailing dedup window
- **THEN** both appear as separate rows in the feed

#### Scenario: Dedup key is 64-bit
- **WHEN** a dedup key is computed for any envelope
- **THEN** the key is a 64-bit value, not the previous 32-bit `HashCode`-derived value

### Requirement: No In-View Header
The live feed view SHALL NOT render its own heading text or divider line; it SHALL rely on its
host container's own title and border for framing, so that the view never duplicates a title
already shown by whatever it is hosted in.

#### Scenario: Feed view renders without a redundant heading
- **WHEN** the live feed view is displayed inside its host frame (titled "0:Live Feed")
- **THEN** the view shows no additional heading text or divider line of its own, and its message
  list starts at the top row of the view's content area

### Requirement: Alt+0 Focuses the Live Feed
The system SHALL allow the user to move keyboard focus directly into the Live Feed pane from
anywhere in the application via a dedicated `Alt+0` shortcut, mirroring how `Alt+1..5` move focus
into a management tab's content.

#### Scenario: Alt+0 focuses the feed from a management tab
- **WHEN** keyboard focus is within a management tab's content and the user presses `Alt+0`
- **THEN** keyboard focus moves into the Live Feed pane

#### Scenario: Alt+0 focuses the feed from anywhere focus can reach
- **WHEN** keyboard focus is anywhere in the application that is part of the normal (non-modal)
  key dispatch chain and the user presses `Alt+0`
- **THEN** keyboard focus moves into the Live Feed pane

### Requirement: Clear Shortcut Is Discoverable
The Live Feed view SHALL advertise its `Clear` shortcut (`C`) through the same opt-in
shortcut-source contract every other shortcut-advertising view uses, so it is included when
shortcuts are aggregated for the currently focused view.

#### Scenario: Clear appears in the shortcut picker while the feed is focused
- **WHEN** keyboard focus is on the Live Feed pane and the user opens the shortcut picker (`?`)
- **THEN** the picker lists `Clear` among the available shortcuts

#### Scenario: Clear is not shown as a separate always-visible status-bar widget
- **WHEN** the Live Feed pane is focused
- **THEN** the status bar does not display a dedicated, always-visible `Clear` widget outside of
  the shortcut picker

### Requirement: Identity-Preserving Selection Across Rollover
When the ring buffer evicts messages from the front to stay within its cap, the system SHALL
shift a selection that is not on the evicted row down by one row per eviction, so a selected
message remains selected - at whatever row it has shifted to - until it is itself the message
evicted. Once the selected message is the one evicted, the system SHALL leave selection on row 0,
landing on whatever message is newly the oldest surviving message after that eviction; no other
message remains to track.

#### Scenario: Selection follows the same message through partial rollover
- **WHEN** the user has selected a message that is not the newest, and enough new messages arrive
  to evict messages older than the selected one but not the selected one itself
- **THEN** the same message remains selected afterward, at whatever row index it has shifted to

#### Scenario: Selection lands on the new oldest message once its own message is evicted
- **WHEN** the user's selected message is itself the next one evicted, because it was the oldest
  surviving message at the moment the buffer next exceeds its cap
- **THEN** selection lands on whatever message is newly at row 0 after that eviction

### Requirement: Clear Resets Selection
The system SHALL clear the list selection whenever `Clear` empties the message list, so no
highlight is left rendered at a stale row index once the list is empty.

#### Scenario: Selection is cleared along with the message list
- **WHEN** the user presses `Clear` (`C`) while a message is selected
- **THEN** the message list becomes empty and no row remains highlighted

### Requirement: Explicit Follow/Pause State
The system SHALL track whether the live feed is following (moving the selection to each newly
arrived message) as an explicit state, not inferred from whether the current selection happens to
occupy the last row. Manual navigation SHALL always pause following, regardless of which row it
lands on, including the last row. `Space` SHALL be the sole way to resume following: pressing it
while paused SHALL move the selection to the newest currently-buffered message and resume
following; pressing it while already following SHALL pause without moving the selection. While
following, each newly arrived message SHALL move the selection to it. While paused, newly arrived
messages SHALL NOT move the selection.

#### Scenario: Manual navigation pauses following even when it lands on the newest message
- **WHEN** the feed is following and the user navigates (e.g. presses Down or End) such that the
  selection lands on the newest message
- **THEN** following is paused, and a subsequently arriving message does not move the selection

#### Scenario: Space pauses in place while following
- **WHEN** the feed is following and the user presses `Space`
- **THEN** following is paused and the current selection does not move

#### Scenario: Space resumes and jumps to the newest message while paused
- **WHEN** the feed is paused and the user presses `Space`
- **THEN** following resumes and the selection moves to the newest currently-buffered message

#### Scenario: New messages move the selection while following
- **WHEN** the feed is following and a new message arrives
- **THEN** the selection moves to that newly arrived message

#### Scenario: New messages do not move the selection while paused
- **WHEN** the feed is paused and a new message arrives
- **THEN** the selection does not move

### Requirement: Vertical Scrollbar on Feed List
The live feed's message list SHALL show a vertical scrollbar reflecting the selection's position
within the currently buffered messages, so the user has a visual sense of position within the
buffer while paused and browsing.

#### Scenario: Scrollbar reflects position while paused and browsing
- **WHEN** the feed is paused and the user has selected a message partway through the buffered
  messages
- **THEN** a vertical scrollbar is visible on the feed list, indicating that position

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

### Requirement: Selecting a Message Opens the Message Detail Dialog
The system SHALL open the Message Detail dialog when the user accepts (selects, e.g. via `Enter`)
a message row in the Live Feed, showing that message's full subject, headers, and payload,
replacing the previous subject-only confirmation stub.

#### Scenario: Accepting a feed row opens the detail dialog
- **WHEN** the Live Feed pane is focused, a message row is selected, and the user accepts it
  (e.g. presses `Enter`)
- **THEN** the Message Detail dialog opens for that message

### Requirement: Row Payload Text Uses Classified Single-Line Presentation
The live feed row for a message SHALL render its payload using the single-line presentation mode
(per `payload-presentation`), driven by the payload's `payload-content-probe` classification, with
a maximum body length of 1024 characters — rather than assuming the payload is valid UTF-8 text
and decoding it unconditionally.

#### Scenario: A JSON payload's row shows the minified, type-prefixed form
- **WHEN** a message with a payload classified as `Json` is shown as a live feed row
- **THEN** the row's payload text is that payload rendered under the single-line presentation
  mode's `Json` case (minified, prefixed `(json) `), not the raw received bytes

#### Scenario: A binary payload's row shows hex instead of decode noise
- **WHEN** a message with a payload that is not well-formed UTF-8 is shown as a live feed row
- **THEN** the row's payload text is that payload rendered under the single-line presentation
  mode's `Binary` case (unspaced hex, prefixed `(blob) `), not the result of a lossy UTF-8 decode

#### Scenario: A large payload's row is capped at 1024 characters of body text
- **WHEN** a message's payload, rendered under the single-line presentation mode, would produce a
  body longer than 1024 characters
- **THEN** the row's payload text body is capped at 1024 characters, with the type prefix present
  in addition to that cap

### Requirement: Payload Classification and Rendering Are Cached Per Envelope, Computed Lazily
The system SHALL compute a message's payload classification and its rendered single-line row text
at most once per `FeedEnvelope`, caching both on the envelope the first time that envelope's row
is actually rendered — not when the message is received into the feed pipeline — so that
classification/rendering cost scales with how many distinct messages are actually scrolled into
view, not with total feed throughput or buffer size.

#### Scenario: An unviewed message's payload is never classified or rendered
- **WHEN** a message arrives in the live feed and is added to the buffer, but its row is never
  scrolled into view before being evicted
- **THEN** its payload is never classified and no single-line row text is ever computed for it

#### Scenario: A viewed message's payload is classified and rendered exactly once
- **WHEN** a message's row is rendered multiple times across separate redraws (e.g. it remains
  visible while other rows above it change)
- **THEN** its payload classification and rendered row text are computed on the first such render
  and reused, unchanged, on every subsequent render of that row

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
