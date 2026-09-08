## ADDED Requirements

### Requirement: KV Value Detail Dialog Shows Key, Metadata, and Value
The system SHALL provide a read-only dialog, opened from the Values tab's key-level list, that
displays a KV entry's key, bucket, revision, creation time, and operation, together with its
value, so a user can inspect a value's full content rather than only the clipped peek visible in
the key detail panel. The key SHALL be shown in its own section, distinct from the remaining
metadata fields.

#### Scenario: Dialog shows the entry's key in its own section
- **WHEN** the KV Value Detail dialog is opened for a key
- **THEN** the dialog displays that entry's key in a dedicated section separate from its other
  metadata

#### Scenario: Dialog shows the entry's remaining metadata
- **WHEN** the KV Value Detail dialog is opened for a key
- **THEN** the dialog displays that entry's bucket, revision, creation time, and operation

#### Scenario: Dialog shows the entry's value
- **WHEN** the KV Value Detail dialog is opened for a key
- **THEN** the dialog displays that entry's value payload

### Requirement: Value Rendered According to a Selectable Presentation Value
The system SHALL classify the entry's value using the payload-content-probe capability to
determine a default presentation value and the set of presentation values valid for that value
(per the payload-presentation capability, expressed as `PayloadType` values), and SHALL render the
value according to the currently selected value and the value section's available width: `Json`
SHALL be pretty-printed with indentation, `Text` SHALL be shown as decoded text wrapped to the
available width, `Hex` SHALL be shown as a hexadecimal byte representation sized to the available
width, and `Base64` SHALL be shown as a base64 representation wrapped to the available width.

#### Scenario: JSON value defaults to pretty-printed presentation
- **WHEN** the KV Value Detail dialog is opened for a key whose value probes as `Json`
- **THEN** the value is initially displayed re-formatted with indentation, even if the stored
  value was minified (no insignificant whitespace)

#### Scenario: Plain text value defaults to as-is presentation, wrapped to a fixed width
- **WHEN** the KV Value Detail dialog is opened for a key whose value probes as `Utf8Text`
- **THEN** the value is initially displayed as its decoded text, unmodified apart from being
  chunked into rows of the value section's available width

#### Scenario: Binary value defaults to hex presentation, not decoded text
- **WHEN** the KV Value Detail dialog is opened for a key whose value probes as `Binary`
- **THEN** the value is initially displayed as a hexadecimal byte representation sized to the
  value section's available width, and the dialog does not attempt to decode and display it as
  text

### Requirement: Value Section's Available Width Is Computed Once At Open
The system SHALL compute the value section's available width once when the KV Value Detail dialog
opens, reserving space for both the value section's vertical scrollbar (regardless of whether that
scrollbar ends up shown for the current presentation) and a one-column visual gap between rendered
content and that reserved scrollbar column, supply that same width to every `Hex`/`Base64`/`Text`
rendering performed for the dialog's lifetime (including subsequent presentation-selector
changes), and SHALL NOT recompute it in response to the terminal being resized while the dialog
remains open.

#### Scenario: A later presentation switch reuses the width computed at open
- **WHEN** the KV Value Detail dialog is open and the user switches the presentation selector to
  `Hex`, `Base64`, or `Text` after having switched away from it
- **THEN** the row/line width used is the same one computed when the dialog was first opened, not
  a value recomputed at the time of the switch

#### Scenario: Resizing the terminal while the dialog is open does not change existing Hex/Base64/Text width
- **WHEN** the KV Value Detail dialog is open showing a `Hex`, `Base64`, or `Text` presentation and
  the terminal is resized
- **THEN** the row/line width already in use does not change

### Requirement: Value Content Is Scrollable
The system SHALL present the rendered value in a scrollable view, so a value longer than its
section's visible area remains fully reachable rather than being clipped, regardless of which
presentation value is currently selected.

#### Scenario: A value longer than the visible area can be scrolled
- **WHEN** the KV Value Detail dialog is opened for a key whose rendered value exceeds the value
  section's visible height
- **THEN** the user can scroll to reach the remaining content

### Requirement: Value Presentation Is Selectable, Limited to Valid Values
The system SHALL provide a control in the KV Value Detail dialog's value section that lets the
user select among the `PayloadType` values valid for that entry's value (per the
payload-presentation capability), and SHALL NOT offer a value that is not valid for that value's
content classification.

#### Scenario: Json-classified value offers all four values
- **WHEN** the KV Value Detail dialog is opened for a key whose value probes as `Json`
- **THEN** the presentation selector offers `Json`, `Text`, `Hex`, and `Base64`

#### Scenario: Binary-classified value offers only Hex and Base64
- **WHEN** the KV Value Detail dialog is opened for a key whose value probes as `Binary`
- **THEN** the presentation selector offers exactly `Hex` and `Base64`

### Requirement: Selecting a Presentation Value Re-Renders the Value In Place
The system SHALL re-render the value section's content to match the newly selected presentation
value when the user changes the selector's value, without closing or reopening the dialog.

#### Scenario: Changing the selector updates the displayed value
- **WHEN** the KV Value Detail dialog is open and the user selects a different presentation value
  from the selector
- **THEN** the value section immediately displays the value rendered under the newly selected
  value, and the dialog remains open

### Requirement: Presentation Selector Does Not Claim Initial Focus
The system SHALL NOT give the presentation selector keyboard focus when the KV Value Detail dialog
opens; the selector SHALL only be reachable by a deliberate keyboard action, and SHALL be
discoverable through the dialog's own shortcut list.

#### Scenario: Opening the dialog does not focus the selector
- **WHEN** the KV Value Detail dialog is opened for a key with a non-empty value
- **THEN** the presentation selector does not have keyboard focus, and the dialog's own scrolling
  and closing behavior work exactly as if the selector did not exist

#### Scenario: A dedicated key summons the selector
- **WHEN** the KV Value Detail dialog is open and the user presses the presentation selector's
  shortcut key
- **THEN** the selector receives keyboard focus and its list of valid presentation values opens

#### Scenario: An empty value advertises no presentation shortcut
- **WHEN** the KV Value Detail dialog is open for a key with an empty value and the user requests
  the shortcut list
- **THEN** no presentation-selector shortcut is listed, since no selector exists for that value

### Requirement: Dialog Is Read-Only
The system SHALL NOT allow editing, copying-out, or writing the value from within the KV Value
Detail dialog; it exists solely to display the entry's content.

#### Scenario: Dialog content cannot be edited
- **WHEN** the KV Value Detail dialog is open and focused
- **THEN** no action within the dialog modifies the underlying key's value, and no
  edit/write affordance is offered

### Requirement: Escape Closes the Dialog
The system SHALL close the KV Value Detail dialog when the user presses `Esc`, with no
confirmation step, consistent with other read-only/cancel-only dialogs in the application.

#### Scenario: Esc closes the dialog
- **WHEN** the KV Value Detail dialog is open and the user presses `Esc`
- **THEN** the dialog closes immediately

### Requirement: Dialog Reuses the Key Detail Panel's Already-Fetched Entry
The system SHALL open the KV Value Detail dialog using the entry data the key detail panel has
already fetched for the highlighted key, rather than issuing a fresh fetch when the dialog is
opened.

#### Scenario: Opening the dialog does not issue a new fetch
- **WHEN** the user opens the KV Value Detail dialog for a key whose entry the detail panel has
  already fetched
- **THEN** the dialog displays that already-fetched entry immediately, without waiting on or
  triggering an additional server round-trip

### Requirement: No Dialog Without a Loaded Entry
The system SHALL NOT open the KV Value Detail dialog when no key is highlighted, or when the
highlighted key's entry has not yet been fetched by the detail panel.

#### Scenario: Shortcut pressed with no key highlighted
- **WHEN** the key-level list is empty (no key highlighted) and the user presses the dialog's
  shortcut key
- **THEN** no dialog opens

#### Scenario: Shortcut pressed before the initial fetch completes
- **WHEN** a key was just highlighted and the detail panel's fetch for it has not yet completed,
  and the user presses the dialog's shortcut key
- **THEN** no dialog opens
