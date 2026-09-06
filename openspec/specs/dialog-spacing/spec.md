# dialog-spacing Specification

## Purpose
Give modal dialogs (`Dialog<T>` subclasses) and `MessageBox` prompts leading and trailing
horizontal space around their title and body/message text, so text never renders flush against
the border — the modal-dialog counterpart to the existing bordered-container title-spacing
convention already documented in CLAUDE.md for `Window`/`FrameView`.

## Requirements

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

### Requirement: Modal dialog content top spacing
A `Dialog<T>` subclass that has a button row SHALL give its content area one blank row between
the dialog's title/border and its first field or control, matching the blank row that already
separates the last field from the button row — so its content reads as symmetrically sparse
rather than cramped at the top and airy at the bottom. A `Dialog<T>` subclass with no button row
(a single-field dialog that commits on Enter) is exempt: with no bottom blank row to balance
against, it stays compact rather than gaining unmatched top padding.

#### Scenario: A multi-field create dialog has a blank row above its first label
- **WHEN** `CreateStreamDialog`, `CreateConsumerDialog`, or `CreateBucketDialog` is opened
- **THEN** one blank row separates the dialog's top border from the "Name" label, matching the
  blank row already present above the button row

#### Scenario: A single-field, button-less dialog stays compact
- **WHEN** `PatternDialog` or `HeaderDialog` is opened
- **THEN** no blank row is added above its field's label — the dialog remains as compact
  vertically as it is today
