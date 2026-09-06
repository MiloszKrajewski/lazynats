## MODIFIED Requirements

### Requirement: Payload Rendered According to a Selectable Presentation Value
The system SHALL classify the message's payload using the payload-content-probe capability to
determine a default presentation value and the set of presentation values valid for that payload
(per the payload-presentation capability, expressed as `PayloadType` values), and SHALL render the
payload according to the currently selected value and the payload section's available width: `Json`
SHALL be pretty-printed with indentation, `Text` SHALL be shown as decoded text wrapped to the
available width (chunked to a fixed character count, not word boundaries), `Hex` SHALL be shown as
a hexadecimal byte representation sized to the available width, and `Base64` SHALL be shown as a
base64 representation wrapped to the available width.

#### Scenario: JSON payload defaults to pretty-printed presentation
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Json`
- **THEN** the payload is initially displayed re-formatted with indentation, even if the original
  wire payload was minified (no insignificant whitespace)

#### Scenario: Plain text payload defaults to as-is presentation, wrapped to a fixed width
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Utf8Text`
- **THEN** the payload is initially displayed as its decoded text, unmodified apart from being
  chunked into rows of the payload section's available width

#### Scenario: Binary payload defaults to hex presentation, not decoded text
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Binary`
- **THEN** the payload is initially displayed as a hexadecimal byte representation sized to the
  payload section's available width, and the dialog does not attempt to decode and display it as
  text

## ADDED Requirements

### Requirement: Payload Section's Available Width Is Computed Once At Open
The system SHALL compute the payload section's available width once when the Message Detail
dialog opens, reserving space for both the payload section's vertical scrollbar (regardless of
whether that scrollbar ends up shown for the current presentation) and a one-column visual gap
between rendered content and that reserved scrollbar column, supply that same width to every
`Hex`/`Base64`/`Text` rendering performed for the dialog's lifetime (including subsequent
presentation-selector changes), and SHALL NOT recompute it in response to the terminal being
resized while the dialog remains open.

#### Scenario: A later presentation switch reuses the width computed at open
- **WHEN** the Message Detail dialog is open and the user switches the presentation selector to
  `Hex`, `Base64`, or `Text` after having switched away from it
- **THEN** the row/line width used is the same one computed when the dialog was first opened, not
  a value recomputed at the time of the switch

#### Scenario: Resizing the terminal while the dialog is open does not change existing Hex/Base64/Text width
- **WHEN** the Message Detail dialog is open showing a `Hex`, `Base64`, or `Text` presentation and
  the terminal is resized
- **THEN** the row/line width already in use does not change, consistent with the `Json`
  presentation, which also does not re-flow in response to a resize while the dialog is open

#### Scenario: A tall payload's rendered content does not run under the vertical scrollbar
- **WHEN** the Message Detail dialog is open for a payload whose rendered `Hex`, `Base64`, or
  `Text` content exceeds the payload section's fixed visible-line cap, so its vertical scrollbar is
  shown
- **THEN** every rendered row still fits entirely to the left of the scrollbar column, with at
  least one blank column between the row's content and the scrollbar, because the available width
  used to render that content already reserved space for both
