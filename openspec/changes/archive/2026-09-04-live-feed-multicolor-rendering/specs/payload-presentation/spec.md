## MODIFIED Requirements

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
