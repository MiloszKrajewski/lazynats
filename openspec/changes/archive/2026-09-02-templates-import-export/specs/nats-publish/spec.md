## MODIFIED Requirements

### Requirement: Compose Message Fields
The Publish dialog SHALL provide an editable Subject field, an editable list of header key/value
pairs managed as whole entries via dedicated New/Edit/Delete actions (not per-character inline
fields), a Payload Type field (`Json`, `Text`, `Base64`, or `Hex`, defaulting to `Text`, per
`payload-types`), and a multi-line text Payload field that is directly editable whenever it holds
keyboard focus (see the Payload Is Always Editable requirement), all editable independently before
sending.

#### Scenario: Fields are independently editable
- **WHEN** the user enters a subject, adds header pairs via the header entry dialog, selects a
  Payload Type, and types into Payload
- **THEN** each field reflects its own entered value without affecting the others

#### Scenario: Payload Type defaults to Text
- **WHEN** the Publish dialog opens
- **THEN** the Payload Type field is set to `Text`

### Requirement: Send Validation
The system SHALL disable the Send action whenever the Subject field is empty, or whenever the
Payload field's text is invalid for the currently selected Payload Type (per `payload-types`'
Payload Validation requirement), and SHALL visually flag whichever of those fields is currently
invalid (e.g. distinct color). No other field SHALL block Send.

#### Scenario: Empty subject disables Send
- **WHEN** the Subject field is empty
- **THEN** the Send action is disabled and the Subject field is shown in its invalid visual state

#### Scenario: Payload invalid for the selected Payload Type disables Send
- **WHEN** the Subject field is non-empty and the Payload field's text is invalid for the
  currently selected Payload Type (e.g. Payload Type is `Base64` and the text does not decode as
  base64)
- **THEN** the Send action is disabled and the Payload field is shown in its invalid visual state

#### Scenario: Non-empty subject and valid payload enable Send regardless of other fields
- **WHEN** the Subject field is non-empty and the Payload field's text is valid for the currently
  selected Payload Type, regardless of whether headers are empty
- **THEN** the Send action is enabled

### Requirement: Send Publishes the Message
The system SHALL, when Send is activated, publish a NATS message on the entered subject using the
entered header pairs and the Payload field's text encoded to bytes according to the currently
selected Payload Type (per `payload-types`' Payload Byte Encoding requirement), via the connected
NATS connection.

#### Scenario: Sending publishes with the entered subject, headers, and payload
- **WHEN** the user has entered subject `orders.created`, header pair `tenant`/`acme-co`, Payload
  Type `Text`, payload text `{"id":42}`, and activates Send
- **THEN** a NATS message is published on subject `orders.created` carrying the `tenant: acme-co`
  header and the UTF-8 encoded payload `{"id":42}`

#### Scenario: Sending with Payload Type Base64 publishes the decoded bytes
- **WHEN** the user has entered a valid subject, selected Payload Type `Base64`, entered a valid
  base64 payload string, and activates Send
- **THEN** a NATS message is published carrying that string's decoded byte sequence as its
  payload, not its UTF-8 text bytes

#### Scenario: Sending with Payload Type Hex publishes the decoded bytes
- **WHEN** the user has entered a valid subject, selected Payload Type `Hex`, entered a valid
  hex-encoded payload string, and activates Send
- **THEN** a NATS message is published carrying that string's hex-decoded byte sequence as its
  payload, not its UTF-8 text bytes
