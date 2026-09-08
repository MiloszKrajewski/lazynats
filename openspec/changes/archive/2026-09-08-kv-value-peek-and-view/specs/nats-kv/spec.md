## MODIFIED Requirements

### Requirement: Key Detail Panel
The system SHALL show, alongside the key list, a detail panel for the currently highlighted key,
presenting its revision, creation time, and operation kind, together with its value classified via
the payload-content-probe capability and rendered accordingly, filling the remainder of the
panel's available space: a `Json`-classified value SHALL be pretty-printed with indentation, a
`Utf8Text`-classified value SHALL be shown as its decoded UTF-8 text, and a `Binary`-classified
value SHALL be shown as a hexadecimal dump at a fixed 16 bytes per row.

#### Scenario: Highlighting a key shows its details
- **WHEN** the user moves the highlight to a key in the key list
- **THEN** the detail panel shows that key's revision, creation time, operation, and its value
  rendered per its content classification

#### Scenario: Highlighting a key fetches its details immediately, not on the next poll tick
- **WHEN** the user moves the highlight to a key in the key list
- **THEN** the system fetches that key's current entry right away, rather than waiting for the
  periodic detail refresh interval to elapse — matching the immediacy of the bucket, stream, and
  consumer detail panels on their own highlight changes

#### Scenario: No key highlighted
- **WHEN** the key list is empty and no key is highlighted
- **THEN** the detail panel shows no key's details

#### Scenario: Value occupies remaining panel space
- **WHEN** a key's details are shown
- **THEN** the rendered value is shown in the space remaining below the revision/creation/
  operation rows, rather than being constrained to a single line among them

#### Scenario: A value taller than the available space is clipped, not scrolled
- **WHEN** a key's value, once rendered, would require more vertical space than the detail panel
  currently has available
- **THEN** the panel shows as much of the value as fits and does not offer scrolling to reveal the
  rest

#### Scenario: A long line wraps to the panel's width rather than being cut off mid-line
- **WHEN** a key's rendered value contains a line longer than the detail panel's available width
- **THEN** that line is broken at the width boundary and continues on the following row(s) (still
  subject to the panel's overall clip, not scroll, behavior) rather than being cut off at the
  panel's edge

#### Scenario: JSON value renders pretty-printed
- **WHEN** a key's value classifies as `Json`
- **THEN** the detail panel shows it re-serialized with indentation, even if the stored value was
  minified (no insignificant whitespace)

#### Scenario: Plain text value renders as decoded UTF-8 text
- **WHEN** a key's value classifies as `Utf8Text`
- **THEN** the detail panel shows it as its decoded UTF-8 text, unmodified

#### Scenario: Binary value renders as a fixed-width hex dump
- **WHEN** a key's value classifies as `Binary`
- **THEN** the detail panel shows it as a hexadecimal byte representation with 16 bytes per row,
  rather than attempting to decode it as text

## ADDED Requirements

### Requirement: Open KV Value Detail Dialog
The system SHALL allow the user to open the KV Value Detail dialog (per the
kv-value-detail-dialog capability) for the highlighted key from the key-level list via `V`, using
the key detail panel's already-fetched entry for that key.

#### Scenario: V opens the KV Value Detail dialog
- **WHEN** the user presses `V` while the key-level list holds focus and a key is highlighted
  whose entry the detail panel has already fetched
- **THEN** the KV Value Detail dialog opens, showing that entry

#### Scenario: V does nothing without a highlighted key
- **WHEN** the user presses `V` while the key-level list holds focus and no key is highlighted
- **THEN** no dialog opens

#### Scenario: V does nothing before the initial fetch completes
- **WHEN** the user presses `V` immediately after highlighting a key, before the detail panel's
  fetch for it has completed
- **THEN** no dialog opens
