# message-detail-dialog Specification

## Purpose

Provide a read-only dialog that presents a single message's full subject, headers, and payload,
so a user can inspect a message's complete content rather than only the truncated text visible in
a Live Feed row. The payload's classification from the `payload-content-probe` capability picks a
default presentation value, and a presentation selector (per the `payload-presentation`
capability) lets the user switch among the presentation values valid for that payload
(`Json`/`Text`/`Hex`/`Base64`) without leaving the dialog.

## Requirements

### Requirement: Message Detail Dialog Shows Subject, Headers, and Payload
The system SHALL provide a read-only dialog that displays a message's subject, its headers, and
its payload, so a user can inspect a full message rather than only the truncated text visible in
a feed row.

#### Scenario: Dialog shows the message subject
- **WHEN** the Message Detail dialog is opened for a message
- **THEN** the dialog displays that message's subject

#### Scenario: Dialog shows each header
- **WHEN** the Message Detail dialog is opened for a message that has one or more headers
- **THEN** the dialog displays each header's key and value

#### Scenario: Dialog shows an explicit empty state when there are no headers
- **WHEN** the Message Detail dialog is opened for a message with no headers
- **THEN** the dialog displays an explicit indication that the message has no headers, rather
  than an empty or missing headers section

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

### Requirement: Dialog Is Read-Only
The system SHALL NOT allow editing, copying-out, or re-sending the message from within the
Message Detail dialog; it exists solely to display the message's content.

#### Scenario: Dialog content cannot be edited
- **WHEN** the Message Detail dialog is open and focused
- **THEN** no action within the dialog modifies the underlying message content, and no send/edit
  affordance is offered

### Requirement: Escape Closes the Dialog
The system SHALL close the Message Detail dialog when the user presses `Esc`, with no confirmation
step, consistent with other read-only/cancel-only dialogs in the application.

#### Scenario: Esc closes the dialog
- **WHEN** the Message Detail dialog is open and the user presses `Esc`
- **THEN** the dialog closes immediately
