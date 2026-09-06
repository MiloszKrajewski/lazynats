## MODIFIED Requirements

### Requirement: Payload Rendered According to a Selectable Presentation Value
The system SHALL classify the message's payload using the payload-content-probe capability to
determine a default presentation value and the set of presentation values valid for that payload
(per the payload-presentation capability, expressed as `PayloadType` values), and SHALL render the
payload according to the currently selected value: `Json` SHALL be pretty-printed with
indentation, `Text` SHALL be shown as plain decoded text, `Hex` SHALL be shown as a hexadecimal
byte representation, and `Base64` SHALL be shown as a base64 representation.

#### Scenario: JSON payload defaults to pretty-printed presentation
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Json`
- **THEN** the payload is initially displayed re-formatted with indentation, even if the original
  wire payload was minified (no insignificant whitespace)

#### Scenario: Plain text payload defaults to as-is presentation
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Utf8Text`
- **THEN** the payload is initially displayed as its decoded text, unmodified

#### Scenario: Binary payload defaults to hex presentation, not decoded text
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Binary`
- **THEN** the payload is initially displayed as a hexadecimal byte representation, and the dialog
  does not attempt to decode and display it as text

### Requirement: Payload Content Is Scrollable
The system SHALL present the rendered payload in a scrollable view, so a payload longer than its
section's visible area remains fully reachable rather than being clipped, regardless of which
presentation value is currently selected.

#### Scenario: A payload longer than the visible area can be scrolled
- **WHEN** the Message Detail dialog is opened for a message whose rendered payload exceeds the
  payload section's visible height
- **THEN** the user can scroll to reach the remaining content

#### Scenario: Switching to a taller presentation remains scrollable
- **WHEN** the user selects a presentation value whose rendering exceeds the payload section's
  visible height, whether or not the previously selected value also exceeded it
- **THEN** the user can scroll to reach the remaining content

## ADDED Requirements

### Requirement: Payload Presentation Is Selectable, Limited to Valid Values
The system SHALL provide a control in the Message Detail dialog's payload section that lets the
user select among the `PayloadType` values valid for that message's payload (per the
payload-presentation capability), and SHALL NOT offer a value that is not valid for that payload's
content classification.

#### Scenario: Json-classified payload offers all four values
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Json`
- **THEN** the presentation selector offers `Json`, `Text`, `Hex`, and `Base64`

#### Scenario: Utf8Text-classified payload does not offer Json
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Utf8Text`
- **THEN** the presentation selector offers `Text`, `Hex`, and `Base64`, and does not offer `Json`

#### Scenario: Binary-classified payload offers only Hex and Base64
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Binary`
- **THEN** the presentation selector offers exactly `Hex` and `Base64`

### Requirement: Selecting a Presentation Value Re-Renders the Payload In Place
The system SHALL re-render the payload section's content to match the newly selected presentation
value when the user changes the selector's value, without closing or reopening the dialog.

#### Scenario: Changing the selector updates the displayed payload
- **WHEN** the Message Detail dialog is open and the user selects a different presentation value
  from the selector
- **THEN** the payload section immediately displays the payload rendered under the newly selected
  value, and the dialog remains open

### Requirement: Presentation Selector Does Not Claim Initial Focus
The system SHALL NOT give the presentation selector keyboard focus when the Message Detail dialog
opens; the selector SHALL only be reachable by a deliberate keyboard action, and SHALL be
discoverable through the dialog's own shortcut list.

#### Scenario: Opening the dialog does not focus the selector
- **WHEN** the Message Detail dialog is opened for a message with a non-empty payload
- **THEN** the presentation selector does not have keyboard focus, and the dialog's own scrolling
  and closing behavior work exactly as if the selector did not exist

#### Scenario: A dedicated key summons the selector
- **WHEN** the Message Detail dialog is open and the user presses the presentation selector's
  shortcut key
- **THEN** the selector receives keyboard focus and its list of valid presentation values opens

#### Scenario: Selecting a value returns focus away from the selector
- **WHEN** the user selects a presentation value from the selector's list
- **THEN** keyboard focus returns to the dialog and the selector is no longer reachable except by
  pressing the shortcut key again - the dialog's own scrolling and closing behavior work exactly
  as if the selector did not exist, same as before the shortcut was first pressed

#### Scenario: The selector's shortcut is discoverable
- **WHEN** the Message Detail dialog is open for a message with a non-empty payload and the user
  requests the shortcut list
- **THEN** the presentation selector's shortcut is listed

#### Scenario: An empty payload advertises no presentation shortcut
- **WHEN** the Message Detail dialog is open for a message with an empty payload and the user
  requests the shortcut list
- **THEN** no presentation-selector shortcut is listed, since no selector exists for that message

### Requirement: Presentation Selector Does Not Compromise Read-Only Behavior
The system SHALL treat the presentation selector as a display-only control: changing its value
SHALL NOT modify the underlying message, its subject, its headers, or its raw payload bytes.

#### Scenario: Changing presentation does not alter the message
- **WHEN** the user changes the presentation selector's value
- **THEN** the message's subject, headers, and underlying payload bytes are unchanged
