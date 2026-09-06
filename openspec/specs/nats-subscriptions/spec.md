# nats-subscriptions Specification

## Purpose
Manage the set of active NATS subject-pattern subscriptions that feed the live message feed: listing what's currently active, adding new subject-pattern subscriptions, and deleting them.
## Requirements
### Requirement: Subscription List Management
The system SHALL provide a Subscriptions screen listing the currently active subject-pattern subscriptions (e.g. `invoices.>`), so the user can see at a glance which patterns are currently being monitored.

#### Scenario: Active subscriptions are listed
- **WHEN** one or more subject-pattern subscriptions are active
- **THEN** the Subscriptions screen lists each active pattern

### Requirement: Add Subscription
The system SHALL allow the user to add a new subject-pattern subscription, which SHALL be assigned a new `Guid` subscription identity, start a real NATS subscription (`NatsConnection.SubscribeAsync<byte[]>`) for that pattern, and begin contributing received messages — tagged with that subscription identity and their receipt time — into the shared feed pipeline.

#### Scenario: Adding a pattern starts a real subscription
- **WHEN** the user adds subject pattern `invoices.>`
- **THEN** a new subscription identity is assigned, a NATS subscription for `invoices.>` is started against the connected `NatsConnection`, and messages published on matching subjects begin arriving in the feed tagged with that subscription's identity

#### Scenario: Re-adding a pattern gets a new identity
- **WHEN** a subscription for pattern `invoices.>` is deleted and then a subscription for the same pattern `invoices.>` is added again
- **THEN** the newly added subscription is assigned a different subscription identity than the one that was deleted

### Requirement: Delete Subscription
The system SHALL allow the user to delete an active subscription, which SHALL stop its underlying NATS subscription so no further messages for that pattern are received.

#### Scenario: Deleting a pattern stops receiving its messages
- **WHEN** the user deletes an active subscription for pattern `invoices.>`
- **THEN** its background reader task and NATS subscription are cancelled/disposed, and messages published afterward on subjects matching `invoices.>` (and not matched by any other still-active pattern) no longer appear in the feed

### Requirement: Modifying a Subscription Pattern
The system SHALL provide a Ctrl+E convenience for changing an active subscription's pattern: it SHALL
open a modal pre-filled with the selected subscription's pattern for editing, and on commit SHALL
remove the existing subscription and add a new one with the edited pattern. This SHALL NOT preserve
the subscription's identity — the new subscription SHALL be assigned a different `Guid` than the one
it replaces — since a NATS subscription cannot be altered in place. The same effect SHALL also
remain achievable via separate delete and add actions, without using Ctrl+E. The modal SHALL have no
buttons: Enter SHALL commit the current pattern text as long as it is non-empty (Enter SHALL be a
no-op while the pattern is empty), and Esc SHALL cancel, leaving the subscription unchanged.

#### Scenario: Ctrl+E opens a modal pre-filled with the selected pattern
- **WHEN** the user selects an active subscription and presses Ctrl+E
- **THEN** a modal opens pre-filled with that subscription's pattern for editing

#### Scenario: Committing an edit replaces the subscription with a new identity
- **WHEN** the user has loaded a subscription's pattern via Ctrl+E, changes the pattern text, and
  commits it
- **THEN** the original subscription is removed, a new subscription is added for the edited pattern,
  and the new subscription is assigned a different identity than the one it replaced

#### Scenario: Changing a pattern is delete-then-add
- **WHEN** the user wants to change an active subscription's pattern without using Ctrl+E
- **THEN** the user can delete the existing subscription and add a new one with the new pattern, with
  the same effect as using Ctrl+E

#### Scenario: Enter commits a valid pattern with no button involved
- **WHEN** the edit modal is open, the pattern field is non-empty, and the user presses Enter
- **THEN** the modal commits that pattern and closes, without the user having activated any button

#### Scenario: Enter is a no-op while the pattern is empty
- **WHEN** the edit modal is open, the pattern field is empty, and the user presses Enter
- **THEN** the modal remains open and the subscription is unchanged

#### Scenario: Esc cancels and leaves the subscription unchanged
- **WHEN** the edit modal is open and the user presses Esc
- **THEN** the modal closes, the original subscription is left unchanged, and no new subscription is
  added

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

