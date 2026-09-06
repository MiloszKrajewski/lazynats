## ADDED Requirements

### Requirement: Framed Field Presentation
The Publish tab's Subject field, Payload field, and header editor SHALL each be presented
wrapped in a padded `EditFrame`, giving each field visual breathing room without a full bordered
box, and without altering any of their existing editing, validation, or send behavior.

#### Scenario: Subject field is visually framed
- **WHEN** the Publish tab is displayed
- **THEN** the Subject field is presented inside a padded frame, and its existing validation
  behavior (invalid styling while empty, disabling Send) is unchanged

#### Scenario: Payload field is visually framed
- **WHEN** the Publish tab is displayed
- **THEN** the Payload field is presented inside a padded frame, and its existing editing
  behavior is unchanged

#### Scenario: Header editor is visually framed
- **WHEN** the Publish tab is displayed
- **THEN** the header editor is presented inside a padded frame, and its existing New/Edit/Delete
  behavior is unchanged
