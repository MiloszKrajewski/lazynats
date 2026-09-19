# payload-content-probe Specification

## Purpose

Classify raw message-payload bytes into one of `Json`, `Utf8Text`, or `Binary`, independent of the
outbound `PayloadType` concept used to compose payloads for sending, so any feature that displays
an arbitrary message payload (currently the `message-detail-dialog`) can decide how to render it
without re-implementing the detection.

## Requirements

### Requirement: Three-Way Payload Content Classification
The system SHALL provide a component that classifies raw payload bytes into exactly one of three
kinds — `Json`, `Utf8Text`, or `Binary` — usable by any feature that displays an arbitrary
message payload, independent of the outbound `PayloadType` concept used to compose payloads for
sending.

#### Scenario: Valid JSON payload classifies as Json
- **WHEN** the payload bytes are the UTF-8 encoding of valid JSON text (e.g. `{"id":1}`)
- **THEN** the probe classifies it as `Json`

#### Scenario: Plain UTF-8 text payload classifies as Utf8Text
- **WHEN** the payload bytes are the UTF-8 encoding of text that is not valid JSON (e.g. `hello
  world`)
- **THEN** the probe classifies it as `Utf8Text`

#### Scenario: Non-UTF-8 payload classifies as Binary
- **WHEN** the payload bytes do not form a well-formed UTF-8 sequence
- **THEN** the probe classifies it as `Binary`

#### Scenario: Empty payload classifies as Utf8Text
- **WHEN** the payload has zero bytes
- **THEN** the probe classifies it as `Utf8Text`, not `Binary`

### Requirement: Strict UTF-8 Well-Formedness, Not a Printable-ASCII Heuristic
The system SHALL determine UTF-8-ness by strict decoding (a decode that fails on any malformed or
overlong byte sequence), not by a "looks like printable ASCII" heuristic, so that well-formed
multi-byte UTF-8 text — including non-Latin scripts and emoji — classifies as text rather than
being mistaken for binary merely because it isn't ASCII.

#### Scenario: UTF-8 text containing emoji classifies as text, not binary
- **WHEN** the payload bytes are the UTF-8 encoding of text containing emoji or other non-ASCII,
  multi-byte characters, and contain no other classification-affecting content
- **THEN** the probe classifies it as `Json` or `Utf8Text` (per the JSON/plain-text distinction),
  never `Binary`, solely because the text is non-ASCII

#### Scenario: Malformed byte sequence classifies as Binary even if partially ASCII
- **WHEN** the payload bytes are mostly printable ASCII but contain a byte sequence that is not
  valid UTF-8 (e.g. an isolated continuation byte, or a truncated multi-byte sequence)
- **THEN** the probe classifies it as `Binary`

### Requirement: Well-Formed UTF-8 Containing Control Characters Classifies as Binary
The system SHALL classify a payload that decodes as well-formed UTF-8 but contains C0 or C1
control characters other than tab (`\t`), line feed (`\n`), or carriage return (`\r`) as `Binary`,
not `Json`/`Utf8Text`, so that binary data which happens to be valid UTF-8 (e.g. some
protobuf-encoded payloads) is not misrendered as text full of control-character noise.

#### Scenario: Well-formed UTF-8 with an embedded control character classifies as Binary
- **WHEN** the payload bytes decode as well-formed UTF-8 but the decoded text contains a control
  character other than tab, line feed, or carriage return (e.g. a NUL or other C0 control byte)
- **THEN** the probe classifies it as `Binary`

#### Scenario: Well-formed UTF-8 text using only tab, newline, and carriage return as control characters classifies as text
- **WHEN** the payload bytes decode as well-formed UTF-8 and the only control characters present
  are tab, line feed, and/or carriage return
- **THEN** the probe classifies it as `Json` or `Utf8Text` per the JSON/plain-text distinction,
  not `Binary`

### Requirement: Classification Without Per-Call String Allocation
The system SHALL classify payload bytes without heap-allocating a decoded string proportional to
the payload's length, so that classifying a binary or non-JSON-text payload - both common,
expected outcomes rather than exceptional ones - never carries the cost of a discarded decode
buffer. Malformed UTF-8 is additionally detected without exception-based control flow, since
`System.Text.Unicode.Utf8.IsValid` reports well-formedness directly; the JSON-parse check still
catches `JsonException` internally, because `JsonDocument.Parse` throws (rather than returning
`false`) for structurally invalid JSON - the BCL's public `JsonDocument` surface has no
non-throwing way to detect malformed JSON syntax.

#### Scenario: Malformed UTF-8 is rejected without throwing or decoding
- **WHEN** the payload bytes do not form a well-formed UTF-8 sequence
- **THEN** the probe classifies it as `Binary` without the classification path raising or catching
  an exception, and without allocating a decoded string of the payload

#### Scenario: Non-JSON text is rejected without allocating a decoded string
- **WHEN** the payload bytes are well-formed UTF-8 text that is not valid JSON
- **THEN** the probe classifies it as `Utf8Text` by parsing the UTF-8 bytes directly (via
  `JsonDocument.Parse(ReadOnlyMemory<byte>)`), without ever allocating a decoded string of the
  payload, even though the JSON-parse failure is still detected via a caught `JsonException`
