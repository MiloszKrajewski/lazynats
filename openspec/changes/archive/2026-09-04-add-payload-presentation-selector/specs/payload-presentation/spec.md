## ADDED Requirements

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
value: `Json` SHALL re-serialize the bytes' decoded JSON text with indentation; `Text` SHALL show
the bytes' decoded UTF-8 text unmodified; `Hex` SHALL show a hexadecimal byte representation;
`Base64` SHALL show a base64-encoded representation of the bytes. This rendering is independent of
`PayloadValidation`/`PayloadEncoding` (which validate and encode user-typed text for sending, not
render already-received bytes for display).

#### Scenario: Json value pretty-prints
- **WHEN** payload bytes are rendered under the `Json` presentation value
- **THEN** the display text is the bytes' JSON content re-serialized with indentation, even if the
  original bytes contained no insignificant whitespace

#### Scenario: Text value shows decoded text as-is
- **WHEN** payload bytes are rendered under the `Text` presentation value
- **THEN** the display text is the bytes decoded as UTF-8, unmodified

#### Scenario: Hex value shows a hex byte representation
- **WHEN** payload bytes are rendered under the `Hex` presentation value
- **THEN** the display text is a hexadecimal representation of the raw bytes, not a decoded-text
  interpretation of them

#### Scenario: Base64 value shows a base64 representation
- **WHEN** payload bytes are rendered under the `Base64` presentation value
- **THEN** the display text is a base64 encoding of the raw bytes, not a decoded-text
  interpretation of them

### Requirement: Rendering Under an Invalid Value Is Not a Supported Operation
The system SHALL NOT be required to produce a meaningful rendering when asked to render payload
bytes under a presentation value not in that payload's valid set (e.g. rendering non-JSON bytes
under the `Json` value) — callers are responsible for only offering and requesting values from the
payload's valid set, per the availability requirement above.

#### Scenario: Callers only request valid values
- **WHEN** a caller renders payload bytes for display
- **THEN** it selects the presentation value from that payload's valid set (as derived from its
  content classification), never a value outside that set
