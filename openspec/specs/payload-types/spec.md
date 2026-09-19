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
be valid only if it parses as JSON; `Base64` text SHALL be valid only if, after discarding any
whitespace that falls between complete 4-character quanta, the remaining text decodes as base64 —
whitespace that falls inside a quantum (splitting it) SHALL make the text invalid; `Hex` text
SHALL be valid only if, after discarding any whitespace that falls between complete 2-digit byte
pairs, the remaining text decodes as a hex-encoded byte sequence — whitespace that falls inside a
byte pair (splitting it) SHALL make the text invalid; `Text` SHALL treat all input, including
empty, as valid, with no whitespace handling applied (every character, including whitespace, is
significant).

#### Scenario: Invalid JSON is rejected
- **WHEN** Payload Type is `Json` and the payload text does not parse as valid JSON
- **THEN** the payload is invalid for that type

#### Scenario: Invalid Base64 is rejected
- **WHEN** Payload Type is `Base64` and the payload text, after discarding boundary-aligned
  whitespace, does not decode as valid base64
- **THEN** the payload is invalid for that type

#### Scenario: Invalid hex is rejected
- **WHEN** Payload Type is `Hex` and the payload text, after discarding boundary-aligned
  whitespace, does not decode as a valid hex-encoded byte sequence (e.g. contains non-hex
  characters, or an odd number of hex digits)
- **THEN** the payload is invalid for that type

#### Scenario: Any text is valid for Text
- **WHEN** Payload Type is `Text`
- **THEN** the payload text is always valid, including when empty

#### Scenario: Whitespace between hex byte pairs is ignored
- **WHEN** Payload Type is `Hex` and the payload text is a hex-encoded byte sequence with
  whitespace inserted only between complete 2-digit byte pairs (e.g. `48 65 6C 6C 6F`)
- **THEN** the payload is valid for that type

#### Scenario: Whitespace between base64 quanta is ignored
- **WHEN** Payload Type is `Base64` and the payload text is a valid base64 string with whitespace
  (including line breaks) inserted only between complete 4-character quanta
- **THEN** the payload is valid for that type

#### Scenario: Whitespace splitting a hex byte pair is rejected
- **WHEN** Payload Type is `Hex` and the payload text has whitespace inserted between the two
  digits of a byte pair (e.g. `4 8 65`)
- **THEN** the payload is invalid for that type

#### Scenario: Whitespace splitting a base64 quantum is rejected
- **WHEN** Payload Type is `Base64` and the payload text has whitespace inserted inside a
  4-character quantum rather than between complete quanta
- **THEN** the payload is invalid for that type

### Requirement: Payload Byte Encoding
The system SHALL encode a validated payload's text to the raw bytes it is sent or stored as,
according to its Payload Type: `Json` and `Text` SHALL encode as the UTF-8 bytes of the text
itself, with no whitespace discarded — every character the user entered is part of the encoded
bytes; `Base64` SHALL encode as the decoded byte sequence of the text with any boundary-aligned
whitespace (between complete 4-character quanta) discarded first; `Hex` SHALL encode as the
hex-decoded byte sequence of the text with any boundary-aligned whitespace (between complete
2-digit byte pairs) discarded first.

#### Scenario: Json and Text encode as UTF-8 text
- **WHEN** Payload Type is `Json` or `Text` and the payload text is `{"id":1}`
- **THEN** the encoded bytes are the UTF-8 encoding of `{"id":1}`

#### Scenario: Whitespace in Text is part of the encoded payload
- **WHEN** Payload Type is `Text` and the payload text contains whitespace (e.g. line breaks or
  spaces the user typed)
- **THEN** the encoded bytes include that whitespace exactly as entered, unlike `Hex`/`Base64`
  where boundary-aligned whitespace is discarded before encoding

#### Scenario: Base64 encodes as decoded bytes
- **WHEN** Payload Type is `Base64` and the payload text is a valid base64 string
- **THEN** the encoded bytes are that string's decoded byte sequence, not its UTF-8 text bytes

#### Scenario: Hex encodes as decoded bytes
- **WHEN** Payload Type is `Hex` and the payload text is a valid hex-encoded string
- **THEN** the encoded bytes are that string's hex-decoded byte sequence, not its UTF-8 text bytes

#### Scenario: Whitespace-formatted Base64 encodes identically to its unformatted form
- **WHEN** Payload Type is `Base64` and one payload text is a base64 string with whitespace
  inserted only between complete 4-character quanta, and another payload text is the same string
  with that whitespace removed
- **THEN** both encode to the same bytes

#### Scenario: Whitespace-formatted Hex encodes identically to its unformatted form
- **WHEN** Payload Type is `Hex` and one payload text is a hex-encoded string with whitespace
  inserted only between complete 2-digit byte pairs, and another payload text is the same string
  with that whitespace removed
- **THEN** both encode to the same bytes

### Requirement: Allocation-Free Hex/Base64 Validation
Checking whether a payload's text is valid for the `Hex` or `Base64` Payload Type SHALL NOT
allocate heap memory whose size is proportional to the payload's length. Decoding a validated
payload's text to its raw bytes (Payload Byte Encoding) is unaffected by this requirement, since
its result must be an owned, independently-lived byte array regardless of allocation strategy.

#### Scenario: Validating a long Hex payload does not allocate proportionally to its length
- **WHEN** Payload Type is `Hex` and the payload text is a long (e.g. multi-kilobyte)
  whitespace-formatted hex string
- **THEN** checking its validity does not allocate a heap buffer sized to the payload's length

#### Scenario: Validating a long Base64 payload does not allocate proportionally to its length
- **WHEN** Payload Type is `Base64` and the payload text is a long (e.g. multi-kilobyte)
  whitespace-formatted base64 string
- **THEN** checking its validity does not allocate a heap buffer sized to the payload's length

#### Scenario: Validation and encoding still agree on validity
- **WHEN** the same payload text is checked for validity and, if valid, encoded to bytes
- **THEN** both operations use the same underlying decode rules, so validation never accepts text
  that encoding then fails to decode

### Requirement: Payload Type Logic Lives In A Dependency-Free, Unit-Tested Library
The Payload Type concept - Payload Type selection, Payload Validation, Payload Byte Encoding, and
the sibling payload-content-probe/payload-presentation capabilities that share this code - SHALL
live in the `lazynats.Core` class library project rather than the main `lazynats` application
project. `lazynats.Core` SHALL NOT depend, directly or transitively, on Terminal.Gui or
NATS.Client, so this logic can be exercised by tests without constructing UI or network state.

#### Scenario: Payload logic is testable without UI or network dependencies
- **WHEN** a test project references only `lazynats.Core`
- **THEN** it can validate and encode payload text (e.g. via Payload Validation and Payload Byte
  Encoding) without also referencing Terminal.Gui or NATS.Client

#### Scenario: The library carries no UI or network package reference
- **WHEN** `lazynats.Core`'s package/project references are inspected
- **THEN** none of them is Terminal.Gui, NATS.Client, or a package that itself depends on either
