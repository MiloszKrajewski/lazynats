## ADDED Requirements

### Requirement: Payload Field Word-Wrap
The Payload field SHALL word-wrap its content when the selected Payload Type is `Hex`, `Base64`,
or `Text`, and SHALL NOT word-wrap when it is `Json`. Changing the Payload Type field's value while
the dialog is open SHALL update the Payload field's wrap behavior immediately, without requiring
the dialog to be closed and reopened.

#### Scenario: Hex payload wraps
- **WHEN** Payload Type is `Hex` and the Payload field's text is wider than the field
- **THEN** the text wraps to additional visible lines instead of requiring horizontal scrolling

#### Scenario: Base64 payload wraps
- **WHEN** Payload Type is `Base64` and the Payload field's text is wider than the field
- **THEN** the text wraps to additional visible lines instead of requiring horizontal scrolling

#### Scenario: Text payload wraps
- **WHEN** Payload Type is `Text` and the Payload field's text is wider than the field
- **THEN** the text wraps to additional visible lines instead of requiring horizontal scrolling

#### Scenario: Json payload does not wrap
- **WHEN** Payload Type is `Json` and the Payload field's text is wider than the field
- **THEN** the text does not wrap; the field scrolls horizontally instead

#### Scenario: Switching Payload Type updates wrap without reopening
- **WHEN** the Payload field already holds text and the user changes Payload Type from `Json` to
  `Hex` (or vice versa)
- **THEN** the Payload field's wrap behavior updates to match the newly selected type immediately
