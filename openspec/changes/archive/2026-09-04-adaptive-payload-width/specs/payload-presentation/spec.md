## MODIFIED Requirements

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

## ADDED Requirements

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
