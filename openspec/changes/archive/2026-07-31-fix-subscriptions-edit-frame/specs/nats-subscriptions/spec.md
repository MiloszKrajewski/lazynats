## ADDED Requirements

### Requirement: Framed List Presentation
The Subscribe tab's subscription list SHALL be presented with a "Subscriptions" label above a
padded `EditFrame`, using the same shared editable-control background as the Publish tab's fields
and header editor, giving it visual breathing room, background consistency, and field-name
labeling matching the Publish tab's fields, without altering any of its existing add/edit/delete
behavior.

#### Scenario: Subscription list is visually framed and labeled
- **WHEN** the Subscribe tab is displayed
- **THEN** a "Subscriptions" label is shown above the subscription list, the list is presented
  inside a padded frame using the shared editable background color, and its existing
  New/Edit/Delete behavior is unchanged
