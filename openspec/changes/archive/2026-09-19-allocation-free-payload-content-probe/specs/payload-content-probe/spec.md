## ADDED Requirements

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
