## MODIFIED Requirements

### Requirement: Modifying a Subscription Pattern
The system SHALL provide a Ctrl+E convenience for changing an active subscription's pattern: it
SHALL open a modal pre-filled with the selected subscription's pattern for editing, and on commit
SHALL remove the existing subscription and add a new one with the edited pattern. This SHALL NOT
preserve the subscription's identity — the new subscription SHALL be assigned a different `Guid`
than the one it replaces — since a NATS subscription cannot be altered in place. The same effect
SHALL also remain achievable via separate delete and add actions, without using Ctrl+E.

#### Scenario: Ctrl+E opens a modal pre-filled with the selected pattern
- **WHEN** the user selects an active subscription and presses Ctrl+E
- **THEN** a modal opens pre-filled with that subscription's pattern for editing

#### Scenario: Committing an edit replaces the subscription with a new identity
- **WHEN** the user has opened a subscription's pattern for editing via Ctrl+E, changes the pattern
  text, and commits it
- **THEN** the original subscription is removed, a new subscription is added for the edited
  pattern, and the new subscription is assigned a different identity than the one it replaced

#### Scenario: Changing a pattern is delete-then-add
- **WHEN** the user wants to change an active subscription's pattern without using Ctrl+E
- **THEN** the user can delete the existing subscription and add a new one with the new pattern,
  with the same effect as using Ctrl+E
