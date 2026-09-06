## MODIFIED Requirements

### Requirement: Modifying a Subscription Pattern
The system SHALL provide an E convenience for changing an active subscription's pattern: it SHALL
open a modal pre-filled with the selected subscription's pattern for editing, and on commit SHALL
remove the existing subscription and add a new one with the edited pattern. This SHALL NOT preserve
the subscription's identity — the new subscription SHALL be assigned a different `Guid` than the one
it replaces — since a NATS subscription cannot be altered in place. The same effect SHALL also
remain achievable via separate delete and add actions, without using E. The modal SHALL have no
buttons: Enter SHALL commit the current pattern text as long as it is non-empty (Enter SHALL be a
no-op while the pattern is empty), and Esc SHALL cancel, leaving the subscription unchanged.

#### Scenario: E opens a modal pre-filled with the selected pattern
- **WHEN** the user selects an active subscription and presses E
- **THEN** a modal opens pre-filled with that subscription's pattern for editing

#### Scenario: Committing an edit replaces the subscription with a new identity
- **WHEN** the user has loaded a subscription's pattern via E, changes the pattern text, and
  commits it
- **THEN** the original subscription is removed, a new subscription is added for the edited pattern,
  and the new subscription is assigned a different identity than the one it replaced

#### Scenario: Changing a pattern is delete-then-add
- **WHEN** the user wants to change an active subscription's pattern without using E
- **THEN** the user can delete the existing subscription and add a new one with the new pattern, with
  the same effect as using E

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
