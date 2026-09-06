# message-detail-dialog Specification

## Purpose

Provide a read-only dialog that presents a single message's full subject, headers, and payload,
so a user can inspect a message's complete content rather than only the truncated text visible in
a Live Feed row. The payload is rendered according to its classification from the
`payload-content-probe` capability (pretty-printed JSON, plain UTF-8 text, or hex for binary).

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

### Requirement: Payload Rendered According to Its Probed Content Kind
The system SHALL classify the message's payload using the payload-content-probe capability and
render it accordingly: `Json` payloads SHALL be pretty-printed with indentation, `Utf8Text`
payloads SHALL be shown as plain text, and `Binary` payloads SHALL be shown as a hex
representation rather than attempting to render them as text.

#### Scenario: JSON payload is pretty-printed
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Json`
- **THEN** the payload is displayed re-formatted with indentation, even if the original wire
  payload was minified (no insignificant whitespace)

#### Scenario: Plain text payload is shown as-is
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Utf8Text`
- **THEN** the payload is displayed as its decoded text, unmodified

#### Scenario: Binary payload is shown as hex, not decoded text
- **WHEN** the Message Detail dialog is opened for a message whose payload probes as `Binary`
- **THEN** the payload is displayed as a hexadecimal byte representation, and the dialog does not
  attempt to decode and display it as text

### Requirement: Payload Content Is Scrollable
The system SHALL present the rendered payload in a scrollable view, so a payload longer than its
section's visible area remains fully reachable rather than being clipped.

#### Scenario: A payload longer than the visible area can be scrolled
- **WHEN** the Message Detail dialog is opened for a message whose rendered payload exceeds the
  payload section's visible height
- **THEN** the user can scroll to reach the remaining content

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
