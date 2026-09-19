## ADDED Requirements

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
