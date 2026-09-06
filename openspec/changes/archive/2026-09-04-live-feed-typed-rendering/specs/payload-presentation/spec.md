## ADDED Requirements

### Requirement: Single-Line Presentation For Compact Display
The system SHALL provide a single-line rendering mode, independent of the width-wrapped `Json`/
`Text`/`Hex`/`Base64` presentation values, that renders payload bytes as one type-prefixed,
length-capped line suitable for a display context with limited horizontal space and no line
wrapping (e.g. a feed row), given the payload's `PayloadContentKind` classification and a maximum
body length: `Json` SHALL render the bytes' JSON content re-serialized without indentation
(minified), prefixed `(json) `; `Utf8Text` SHALL render the bytes' decoded UTF-8 text with every
maximal run of whitespace or control characters collapsed to a single space, prefixed `(text) `;
`Binary` SHALL render as a hexadecimal representation of the bytes with no separators between byte
pairs, prefixed `(blob) `. In every case, the body (the rendered text excluding its prefix) SHALL
be capped at the given maximum length; the prefix SHALL NOT count toward that cap.

#### Scenario: Json content renders minified with its prefix
- **WHEN** payload bytes classified as `Json` are rendered under the single-line mode
- **THEN** the result is `(json) ` followed by the JSON content re-serialized with no indentation
  or insignificant whitespace, even if the original bytes were formatted with indentation

#### Scenario: Utf8Text content renders with whitespace collapsed and its prefix
- **WHEN** payload bytes classified as `Utf8Text` are rendered under the single-line mode
- **THEN** the result is `(text) ` followed by the decoded text with every maximal run of
  whitespace or control characters (including but not limited to space, tab, newline, and
  carriage return) replaced by a single space

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
