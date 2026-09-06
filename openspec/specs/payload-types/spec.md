# payload-types

## Purpose

Define a shared payload-type concept — the set of payload types, how a payload's text is
validated against a selected type, and how validated text is encoded to raw bytes — reused by
every feature that composes or stores a message payload (Publish, Templates), rather than each
feature defining its own set independently.

## Requirements

### Requirement: Payload Type Set
The system SHALL define a shared set of four payload types — `Json`, `Text`, `Base64`, and `Hex`
— usable anywhere a message payload is composed or stored, rather than each feature defining its
own set.

#### Scenario: Four payload types are available
- **WHEN** a payload type is selected from a Payload Type field
- **THEN** the available choices are exactly `Json`, `Text`, `Base64`, and `Hex`

### Requirement: Payload Validation
The system SHALL validate a payload's text against its selected Payload Type: `Json` text SHALL
be valid only if it parses as JSON; `Base64` text SHALL be valid only if it decodes as base64;
`Hex` text SHALL be valid only if it decodes as a hex-encoded byte sequence; `Text` SHALL treat
all input, including empty, as valid.

#### Scenario: Invalid JSON is rejected
- **WHEN** Payload Type is `Json` and the payload text does not parse as valid JSON
- **THEN** the payload is invalid for that type

#### Scenario: Invalid Base64 is rejected
- **WHEN** Payload Type is `Base64` and the payload text does not decode as valid base64
- **THEN** the payload is invalid for that type

#### Scenario: Invalid hex is rejected
- **WHEN** Payload Type is `Hex` and the payload text does not decode as a valid hex-encoded byte
  sequence (e.g. contains non-hex characters, or an odd number of hex digits)
- **THEN** the payload is invalid for that type

#### Scenario: Any text is valid for Text
- **WHEN** Payload Type is `Text`
- **THEN** the payload text is always valid, including when empty

### Requirement: Payload Byte Encoding
The system SHALL encode a validated payload's text to the raw bytes it is sent or stored as,
according to its Payload Type: `Json` and `Text` SHALL encode as the UTF-8 bytes of the text
itself; `Base64` SHALL encode as the decoded byte sequence; `Hex` SHALL encode as the
hex-decoded byte sequence.

#### Scenario: Json and Text encode as UTF-8 text
- **WHEN** Payload Type is `Json` or `Text` and the payload text is `{"id":1}`
- **THEN** the encoded bytes are the UTF-8 encoding of `{"id":1}`

#### Scenario: Base64 encodes as decoded bytes
- **WHEN** Payload Type is `Base64` and the payload text is a valid base64 string
- **THEN** the encoded bytes are that string's decoded byte sequence, not its UTF-8 text bytes

#### Scenario: Hex encodes as decoded bytes
- **WHEN** Payload Type is `Hex` and the payload text is a valid hex-encoded string
- **THEN** the encoded bytes are that string's hex-decoded byte sequence, not its UTF-8 text bytes
