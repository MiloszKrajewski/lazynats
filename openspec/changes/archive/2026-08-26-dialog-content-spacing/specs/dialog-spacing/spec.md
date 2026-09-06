## ADDED Requirements

### Requirement: Modal dialog title spacing
Every `Dialog<T>` subclass SHALL give its `Title` a leading and trailing space, so the title text
does not render flush against the dialog's border corners — the modal-dialog counterpart to the
existing `Window`/`FrameView` title-spacing convention (e.g. `" Live Feed "`).

#### Scenario: A create dialog's title is padded
- **WHEN** `CreateStreamDialog` or `CreateConsumerDialog` is opened
- **THEN** its rendered title has one space of margin on both sides between the text and the
  border corners

#### Scenario: A single-field edit dialog's title is padded regardless of caller-supplied text
- **WHEN** `PatternDialog` or `HeaderDialog` is opened with any caller-supplied title (e.g.
  `"New Header"`, `"Edit Subscription"`)
- **THEN** the rendered title has the same one-space margin on both sides, applied by the dialog
  itself rather than requiring each caller to include the padding in its own string

### Requirement: MessageBox title and message spacing
Every `MessageBox.Query`/`ErrorQuery` call SHALL pad both its title and its message text with a
leading and trailing space, so neither renders flush against the box's border — `MessageBox` has
no `Padding.Thickness` of its own, and auto-sizes tightly around whichever of title, message, or
buttons is widest.

#### Scenario: A confirmation prompt's title and message are padded
- **WHEN** a destructive-action confirmation (e.g. "Delete Stream", "Delete Consumer") is shown
  via `MessageBox.Query`
- **THEN** both the rendered title and the rendered message have one space of margin on both
  sides between the text and the border

#### Scenario: An error prompt's title and message are padded
- **WHEN** a failure is reported via `MessageBox.ErrorQuery` (e.g. "Create Stream Failed",
  "Delete Consumer Failed") with a server/exception message as the body
- **THEN** both the rendered title and the rendered message have the same one-space margin,
  regardless of the exception message's own content

#### Scenario: The box grows to fit the padded text
- **WHEN** a padded title or message is longer than the box would have auto-sized to for the
  unpadded text
- **THEN** the box's rendered width grows to fit the padded text without clipping it
