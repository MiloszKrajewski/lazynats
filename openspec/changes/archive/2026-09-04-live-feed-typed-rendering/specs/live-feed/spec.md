## ADDED Requirements

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
