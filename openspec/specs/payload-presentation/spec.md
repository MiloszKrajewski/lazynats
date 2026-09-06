# payload-presentation Specification

## Purpose

Define, in terms of the existing `PayloadType` enum (`Json`, `Text`, `Base64`, `Hex`, already
defined by `payload-types` for the outbound compose/validate/encode path), which presentation
values are valid for rendering a received payload given its probed `PayloadContentKind`
classification (from `payload-content-probe`), which value is the default, and how to render
payload bytes as display text under a selected value. This is the display-side counterpart to
`payload-types`' compose/validate/encode: it reuses the same enum rather than duplicating it, and
is consumed by `message-detail-dialog`'s presentation selector.

## Requirements

### Requirement: Presentation Reuses the Existing Payload Type Set
The system SHALL express a payload's display presentation using the same four-value `PayloadType`
set already defined by `payload-types` (`Json`, `Text`, `Base64`, `Hex`), rather than defining a
second, separate set of presentation values — this capability is the display-side counterpart to
`payload-types`' compose/validate/encode, reusing its type rather than duplicating it.

#### Scenario: Presentation values are the existing PayloadType values
- **WHEN** a payload's available or selected presentation is referred to
- **THEN** it is one of the same four `PayloadType` values used for composing an outbound payload
  — `Json`, `Text`, `Base64`, or `Hex` — not a distinct type

### Requirement: Presentation Availability Is Derived From Content Classification
The system SHALL determine which `PayloadType` values are valid for rendering a given payload from
its `PayloadContentKind` classification (as produced by `payload-content-probe`): a payload
classified `Json` SHALL allow `Json`, `Text`, `Hex`, and `Base64`; a payload classified `Utf8Text`
SHALL allow `Text`, `Hex`, and `Base64` but not `Json`; a payload classified `Binary` SHALL allow
only `Hex` and `Base64`.

#### Scenario: Json-classified payload allows all four values
- **WHEN** a payload's content classification is `Json`
- **THEN** its valid presentation values are `Json`, `Text`, `Hex`, and `Base64`

#### Scenario: Utf8Text-classified payload excludes Json
- **WHEN** a payload's content classification is `Utf8Text`
- **THEN** its valid presentation values are `Text`, `Hex`, and `Base64`, and `Json` is not among
  them

#### Scenario: Binary-classified payload allows only Hex and Base64
- **WHEN** a payload's content classification is `Binary`
- **THEN** its valid presentation values are exactly `Hex` and `Base64`

### Requirement: Default Presentation Matches Content Classification
The system SHALL select a default presentation value from a payload's content classification:
`Json` classification defaults to `Json`, `Utf8Text` classification defaults to `Text`, `Binary`
classification defaults to `Hex`.

#### Scenario: Json payload defaults to Json presentation
- **WHEN** a payload's content classification is `Json`
- **THEN** its default presentation value is `Json`

#### Scenario: Utf8Text payload defaults to Text presentation
- **WHEN** a payload's content classification is `Utf8Text`
- **THEN** its default presentation value is `Text`

#### Scenario: Binary payload defaults to Hex presentation
- **WHEN** a payload's content classification is `Binary`
- **THEN** its default presentation value is `Hex`

### Requirement: Payload Bytes Render According to a Selected Presentation Value
The system SHALL render payload bytes as display text according to a selected `PayloadType`
value and an available width: `Json` SHALL re-serialize the bytes' decoded JSON text with
indentation; `Text` SHALL show the bytes' decoded UTF-8 text wrapped to fit the available width,
each row chunked to a fixed character count rather than at word boundaries; `Hex` SHALL show a
hexadecimal byte representation with rows sized to fit the available width; `Base64` SHALL show a
base64-encoded representation of the bytes wrapped to fit the available width. `Json` rendering
SHALL NOT depend on the available width. This rendering is independent of
`PayloadValidation`/`PayloadEncoding` (which validate and encode user-typed text for sending, not
render already-received bytes for display).

#### Scenario: Json value pretty-prints
- **WHEN** payload bytes are rendered under the `Json` presentation value
- **THEN** the display text is the bytes' JSON content re-serialized with indentation, even if the
  original bytes contained no insignificant whitespace, regardless of the available width

#### Scenario: Text value shows decoded text wrapped to a fixed character count
- **WHEN** payload bytes are rendered under the `Text` presentation value with a given available
  width
- **THEN** the display text is the bytes decoded as UTF-8, unmodified apart from being chunked into
  rows of that width (not word-wrapped - a row boundary can fall in the middle of a word), including
  when the decoded text has no line breaks of its own (e.g. a minified JSON payload viewed as `Text`)

#### Scenario: Hex value shows a hex byte representation sized to the available width
- **WHEN** payload bytes are rendered under the `Hex` presentation value with a given available
  width
- **THEN** the display text is a hexadecimal representation of the raw bytes, not a decoded-text
  interpretation of them, with each row's byte count sized to fit that width per the Hex Row Width
  requirement

#### Scenario: Base64 value shows a base64 representation wrapped to the available width
- **WHEN** payload bytes are rendered under the `Base64` presentation value with a given available
  width
- **THEN** the display text is a base64 encoding of the raw bytes, not a decoded-text
  interpretation of them, wrapped into lines sized to fit that width per the Base64 Line Width
  requirement

### Requirement: Hex Row Width Adapts To The Available Width
The system SHALL compute the number of bytes shown per `Hex` row from the available width,
selecting the largest candidate from the fixed set `8, 16, 24, 32, 48, 64` bytes whose formatted
row (`XX` byte pairs separated by single spaces, no trailing space) fits within that width, then
clamping the result between a minimum of 8 and a maximum of 64 bytes per row.

#### Scenario: A wide available width selects a larger row size
- **WHEN** the available width comfortably fits more than 16 formatted bytes per row
- **THEN** the `Hex` presentation uses the largest of `8, 16, 24, 32, 48, 64` whose formatted row
  still fits that width

#### Scenario: A narrow available width selects a smaller row size, never below 8
- **WHEN** the available width is too narrow to fit even 8 formatted bytes per row
- **THEN** the `Hex` presentation still uses 8 bytes per row, the minimum, rather than a smaller
  value

#### Scenario: An very wide available width caps at 64 bytes per row
- **WHEN** the available width fits more than 64 formatted bytes per row
- **THEN** the `Hex` presentation uses 64 bytes per row, the maximum, rather than a larger value

### Requirement: Base64 Line Width Adapts To The Available Width
The system SHALL compute the `Base64` line width from the available width, flooring it to the
nearest multiple of 4 characters, then clamping the result between a minimum of 24 and a maximum
of 144 characters per line.

#### Scenario: A wide available width produces wider wrapped lines
- **WHEN** the available width is wide
- **THEN** the `Base64` presentation wraps lines at that width floored to the nearest multiple of
  4, up to a maximum of 144 characters per line

#### Scenario: A narrow available width still wraps at a minimum of 24 characters per line
- **WHEN** the available width is narrower than 24 characters
- **THEN** the `Base64` presentation still wraps lines at 24 characters, the minimum, rather than a
  narrower value

### Requirement: Single-Line Presentation For Compact Display
The system SHALL provide a single-line rendering mode, independent of the width-wrapped `Json`/
`Text`/`Hex`/`Base64` presentation values, that renders payload bytes as one type-prefixed,
length-capped line suitable for a display context with limited horizontal space and no line
wrapping (e.g. a feed row), given the payload's `PayloadContentKind` classification and a maximum
body length: `Json` SHALL render the bytes' JSON content re-serialized without indentation
(minified), prefixed `(json) `; `Utf8Text` with a non-empty decoded body SHALL render the bytes'
decoded UTF-8 text with every maximal run of whitespace or control characters collapsed to a
single space, prefixed `(text) `; `Utf8Text` whose decoded body is empty SHALL instead render as
the literal string `(empty)`, with no separate prefix/body split — this does not introduce a new
`PayloadContentKind`/`PayloadType` value, it is only how the existing `Utf8Text` classification
renders when the decoded text has zero length; `Binary` SHALL render as a hexadecimal
representation of the bytes with no separators between byte pairs, prefixed `(blob) `. In every
case, the body (the rendered text excluding its prefix) SHALL be capped at the given maximum
length; the prefix SHALL NOT count toward that cap.

#### Scenario: Json content renders minified with its prefix
- **WHEN** payload bytes classified as `Json` are rendered under the single-line mode
- **THEN** the result is `(json) ` followed by the JSON content re-serialized with no indentation
  or insignificant whitespace, even if the original bytes were formatted with indentation

#### Scenario: Utf8Text content renders with whitespace collapsed and its prefix
- **WHEN** payload bytes classified as `Utf8Text`, with a non-empty decoded body, are rendered
  under the single-line mode
- **THEN** the result is `(text) ` followed by the decoded text with every maximal run of
  whitespace or control characters (including but not limited to space, tab, newline, and
  carriage return) replaced by a single space

#### Scenario: Empty Utf8Text content renders as (empty)
- **WHEN** payload bytes classified as `Utf8Text` decode to an empty string (zero bytes, or bytes
  that collapse to an empty string) are rendered under the single-line mode
- **THEN** the result is the literal string `(empty)`, not `(text) ` followed by nothing

#### Scenario: Binary content renders as unspaced hex with its prefix
- **WHEN** payload bytes classified as `Binary` are rendered under the single-line mode
- **THEN** the result is `(blob) ` followed by a hexadecimal representation of the bytes with no
  spaces or other separators between byte pairs

#### Scenario: Body length is capped, prefix is not counted
- **WHEN** payload bytes are rendered under the single-line mode with a given maximum body length
- **THEN** the rendered body (excluding the type prefix) does not exceed that maximum length, and
  the prefix is present in full in addition to that length

### Requirement: Binary Single-Line Rendering Is Byte-Budgeted Before Encoding
The system SHALL, when rendering a `Binary`-classified payload under the single-line mode, encode
at most as many bytes as fit within the maximum body length (two hex characters per byte, no
separators) — it SHALL NOT hex-encode the full payload and then truncate the resulting string.

#### Scenario: A large binary payload only has its leading bytes encoded
- **WHEN** a `Binary`-classified payload larger than half the maximum body length is rendered
  under the single-line mode
- **THEN** only the payload's leading bytes — as many as fit within the maximum body length once
  hex-encoded — are ever hex-encoded; the remaining bytes are not encoded at all

#### Scenario: A binary payload smaller than the budget is encoded in full
- **WHEN** a `Binary`-classified payload's hex encoding would not exceed the maximum body length
- **THEN** the entire payload is hex-encoded and the result is not truncated

### Requirement: Rendering Under an Invalid Value Is Not a Supported Operation
The system SHALL NOT be required to produce a meaningful rendering when asked to render payload
bytes under a presentation value not in that payload's valid set (e.g. rendering non-JSON bytes
under the `Json` value) — callers are responsible for only offering and requesting values from the
payload's valid set, per the availability requirement above.

#### Scenario: Callers only request valid values
- **WHEN** a caller renders payload bytes for display
- **THEN** it selects the presentation value from that payload's valid set (as derived from its
  content classification), never a value outside that set
