## MODIFIED Requirements

### Requirement: Add Subscription
The system SHALL allow the user to add a new subscription pattern, which SHALL be compiled via the
shared relaxed filter-expression grammar (`kv-filter-expression`) into a native NATS subject plus
an optional client-side exact-match filter, be assigned a new `Guid` subscription identity, start a
real NATS subscription (`NatsConnection.SubscribeAsync<byte[]>`) for the compiled native subject,
and begin contributing received messages — tagged with that subscription identity and their receipt
time, and (when the pattern isn't natively exact) filtered against the compiled client-side match
first — into the shared feed pipeline.

#### Scenario: Adding a native-exact pattern starts a real subscription
- **WHEN** the user adds subject pattern `invoices.>`
- **THEN** a new subscription identity is assigned, a NATS subscription for `invoices.>` is started
  against the connected `NatsConnection`, and messages published on matching subjects begin
  arriving in the feed tagged with that subscription's identity, with no client-side filtering
  applied

#### Scenario: Adding a relaxed pattern subscribes to its derived native subject and filters client-side
- **WHEN** the user adds subject pattern `inv*ces.paid` (a relaxed pattern, not valid native NATS
  subject syntax)
- **THEN** a new subscription identity is assigned, a NATS subscription is started for that
  pattern's derived native subject (`*.paid`), and only messages whose subject also matches the
  full relaxed pattern are forwarded into the feed tagged with that subscription's identity —
  messages matching `*.paid` but not the full pattern (e.g. `other.paid`) are received from NATS
  but not forwarded

#### Scenario: Re-adding a pattern gets a new identity
- **WHEN** a subscription for pattern `invoices.>` is deleted and then a subscription for the same
  pattern `invoices.>` is added again
- **THEN** the newly added subscription is assigned a different subscription identity than the one
  that was deleted

### Requirement: Modifying a Subscription Pattern
The system SHALL provide an E convenience for changing an active subscription's pattern: it SHALL
open a modal pre-filled with the selected subscription's pattern for editing, and on commit SHALL
remove the existing subscription and add a new one with the edited pattern. This SHALL NOT preserve
the subscription's identity — the new subscription SHALL be assigned a different `Guid` than the one
it replaces — since a NATS subscription cannot be altered in place. The same effect SHALL also
remain achievable via separate delete and add actions, without using E. The modal SHALL have no
buttons: Enter SHALL commit the current pattern text as long as it compiles under the same relaxed
filter-expression grammar used by Add Subscription (Enter SHALL be a no-op while the pattern fails
to compile, including but not limited to being empty), and Esc SHALL cancel, leaving the
subscription unchanged.

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
- **WHEN** the edit modal is open, the pattern field compiles under the shared filter-expression
  grammar, and the user presses Enter
- **THEN** the modal commits that pattern and closes, without the user having activated any button

#### Scenario: Enter is a no-op while the pattern is invalid
- **WHEN** the edit modal is open, the pattern field is empty or otherwise fails to compile under
  the shared filter-expression grammar (e.g. a leading, trailing, or doubled `.`), and the user
  presses Enter
- **THEN** the modal remains open and the subscription is unchanged

#### Scenario: Esc cancels and leaves the subscription unchanged
- **WHEN** the edit modal is open and the user presses Esc
- **THEN** the modal closes, the original subscription is left unchanged, and no new subscription is
  added

## ADDED Requirements

### Requirement: Subscription Failure Reporting
The system SHALL NOT allow a subscription's start-up or background-read failure to go unreported or
crash the application. When starting a subscription's underlying NATS subscription throws, or its
background read loop faults for any reason other than its own cancellation (deletion), the system
SHALL remove that subscription from the active list and report the failure to the user via a
dismissible error dialog.

#### Scenario: A subscription that fails to start is reported and removed
- **WHEN** an added subscription's underlying NATS subscription fails to start (e.g. the connection
  is down, or the server rejects it for a permissions reason)
- **THEN** an error dialog is shown describing the failure, and the subscription no longer appears
  in the active list

#### Scenario: A subscription that faults while running is reported and removed
- **WHEN** an active subscription's background read loop faults for a reason other than the
  subscription being deleted
- **THEN** an error dialog is shown describing the failure, and the subscription no longer appears
  in the active list

#### Scenario: Deleting a subscription is not reported as a failure
- **WHEN** the user deletes an active subscription
- **THEN** its background read loop's resulting cancellation is not reported via an error dialog
