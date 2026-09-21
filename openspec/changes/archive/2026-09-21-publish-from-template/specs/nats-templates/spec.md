## ADDED Requirements

### Requirement: Publish From Template
The system SHALL allow the user to open the Publish dialog pre-populated with the highlighted
template's Subject, Headers, Payload Type, and Payload via P while the Templates list holds focus
and a template is highlighted, per `nats-publish`'s "Publish Dialog" and "Pre-Populated Payload
Rendering" requirements. This is a list-focus-scoped shortcut (bare `P`), distinct from the
existing global Alt+P shortcut that opens the Publish dialog empty from anywhere. The opened
dialog behaves exactly as `nats-publish` specifies: every field remains editable, and Send,
Cancel, and close-on-success behave unchanged. Neither opening the dialog nor sending from it
SHALL modify the template it was opened from.

#### Scenario: P opens a pre-populated Publish dialog
- **WHEN** the user presses P while the Templates list holds focus and a template is highlighted
- **THEN** the Publish dialog opens with its Subject, Headers, Payload Type, and Payload fields
  seeded from that template's current values

#### Scenario: P with no template highlighted does nothing
- **WHEN** the user presses P while the Templates list holds focus and the list is empty (no
  template highlighted)
- **THEN** no Publish dialog opens

#### Scenario: Editing the pre-populated dialog does not change the template
- **WHEN** the user opens the Publish dialog via P on a template, changes the Subject, Headers,
  Payload Type, or Payload field, and either sends or cancels
- **THEN** the highlighted template's stored Subject, Headers, Payload Type, and Payload are
  unchanged
