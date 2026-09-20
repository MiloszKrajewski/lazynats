## ADDED Requirements

### Requirement: Shared Editable Payload Section
The system SHALL provide a single reusable view, composed identically by every dialog that
composes or edits a message payload or KV value (Publish, Create/Edit Template, Create/Edit Key),
consisting of a Payload Type selector and a payload text editor, so that Payload Type/Payload
editing behavior (validation, wrap behavior, invalid-state highlighting, and byte encoding) is
defined once rather than independently by each dialog.

#### Scenario: The same section is used by every payload-editing dialog
- **WHEN** the Publish dialog, the Create/Edit Template dialog, and the Create/Edit Key dialog are
  each opened
- **THEN** each dialog's Payload Type/Payload(Value) editing area is the same shared section, not
  an independently implemented equivalent

### Requirement: Fixed Type Row, Fill-The-Rest Editor Row
The section SHALL lay out as two rows: a Payload Type row (a label plus its dropdown, occupying a
fixed height regardless of the section's own total height) and a payload/value editor row (a label
plus a text editor) that occupies all remaining height within the section.

#### Scenario: Type row height does not change with the section's total height
- **WHEN** the section is given a larger or smaller total height
- **THEN** the Payload Type row's height stays the same, and the payload/value editor row grows or
  shrinks to absorb the difference

### Requirement: Section Adapts To Caller-Supplied Width And Height, With No Internal Limit
The section SHALL size itself to whatever width and height its owning dialog assigns it, and SHALL
NOT impose its own minimum or maximum on either dimension; any such limit (e.g. capping the editor
row's height on a very tall terminal) SHALL be the owning dialog's responsibility, decided before
or independently of the section's own sizing.

#### Scenario: A dialog assigns a fixed width and height
- **WHEN** an owning dialog assigns the section a fixed width and height
- **THEN** the section's Payload Type row and payload/value editor row size themselves to fit
  exactly within that width and height, per the Fixed Type Row requirement

#### Scenario: A dialog assigns a fill-based height
- **WHEN** an owning dialog assigns the section a height that fills available space (rather than a
  fixed value)
- **THEN** the section's payload/value editor row grows to fill that space, with no maximum height
  enforced by the section itself

### Requirement: Payload Type Selector Offers All Values, Unrestricted
The section's Payload Type dropdown SHALL offer all four `PayloadType` values (`Json`, `Text`,
`Base64`, `Hex`) regardless of the payload/value editor's current content, unlike a read-only
presentation selector, which restricts its offered values to what the current content classifies
as.

#### Scenario: All four types are selectable regardless of current content
- **WHEN** the payload/value editor currently holds text that is not valid JSON
- **THEN** the Payload Type dropdown still offers `Json` as a selectable value

### Requirement: Word Wrap Follows Payload Type
The section SHALL disable word wrap in the payload/value editor when the selected Payload Type is
`Json`, and enable it for `Text`, `Base64`, and `Hex`, updating immediately whenever the selected
Payload Type changes.

#### Scenario: Selecting Json disables wrap
- **WHEN** the user selects `Json` as the Payload Type
- **THEN** the payload/value editor's word wrap is disabled

#### Scenario: Selecting a non-Json type enables wrap
- **WHEN** the user selects `Text`, `Base64`, or `Hex` as the Payload Type
- **THEN** the payload/value editor's word wrap is enabled

### Requirement: Validity Reflects Payload Validation, With Invalid-State Highlighting
The section SHALL expose whether its current text is valid for its currently selected Payload
Type (per the `payload-types` capability's Payload Validation requirement), re-evaluating whenever
either the text or the selected Payload Type changes, and SHALL visually highlight the
payload/value editor as invalid whenever it is not.

#### Scenario: Invalid text is highlighted
- **WHEN** the payload/value editor's text is invalid for the currently selected Payload Type
- **THEN** the editor is visually highlighted as invalid, and the section reports itself invalid

#### Scenario: Changing Payload Type re-evaluates validity
- **WHEN** the currently entered text is invalid for the previously selected Payload Type but
  valid for a newly selected one (or vice versa)
- **THEN** the section's reported validity and the editor's highlighting immediately reflect the
  newly selected Payload Type

### Requirement: Encoded Bytes Available Only When Valid
The section SHALL provide the current text encoded to bytes (per the `payload-types` capability's
Payload Byte Encoding requirement) using the currently selected Payload Type, and this SHALL only
be a meaningful operation when the section currently reports itself valid.

#### Scenario: Encoded bytes reflect the current type and text
- **WHEN** the section is valid and its owning dialog reads the encoded bytes
- **THEN** the bytes are the current text encoded under the currently selected Payload Type, per
  Payload Byte Encoding

### Requirement: Seeding From Raw Bytes Renders Per Payload Type
The section SHALL provide a way to seed its Payload Type and payload/value text from an existing
payload's raw bytes and a given Payload Type, rendering the initial display text according to that
type: `Json`, `Hex`, and `Base64` SHALL be rendered via the `payload-presentation` capability's
rendering (at the section's own resolved editor width); `Text` SHALL be seeded by decoding the raw
bytes as plain UTF-8 text, not rendered through `payload-presentation`'s fixed-width line wrapping,
since that wrapping is sized for a non-wrapping read-only display and would double-wrap once
combined with the editor's own word wrap. This rule SHALL be applied identically regardless of
which dialog is seeding the section.

#### Scenario: Seeding a Json payload renders it pretty-printed
- **WHEN** the section is seeded from bytes with Payload Type `Json`
- **THEN** the payload/value editor's initial text is that payload rendered with indentation, even
  if the original bytes contained no insignificant whitespace

#### Scenario: Seeding a Text payload shows the decoded text unwrapped
- **WHEN** the section is seeded from bytes with Payload Type `Text`
- **THEN** the payload/value editor's initial text is the bytes decoded as UTF-8, not chunked to
  any fixed width

#### Scenario: Seeding a Hex or Base64 payload renders it grouped/wrapped for readability
- **WHEN** the section is seeded from bytes with Payload Type `Hex` or `Base64`
- **THEN** the payload/value editor's initial text is that payload rendered per
  `payload-presentation`, grouped into rows/lines sized to the section's own resolved editor width

### Requirement: Seeding From Already-Typed Text Is Verbatim
The section SHALL provide a separate way to seed its Payload Type and payload/value text directly
from already-known display text (not raw bytes), setting that text into the editor unchanged, with
no rendering applied — used when reopening a dialog pre-filled with exactly what the user
previously typed (e.g. after a failed save), or when opening blank for a new Create.

#### Scenario: Reopening after a failed save shows exactly what was typed
- **WHEN** the section is seeded from already-typed text following a failed save
- **THEN** the payload/value editor's text is exactly that previously-typed text, with no
  rendering or reformatting applied
