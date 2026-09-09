## MODIFIED Requirements

### Requirement: Subscription Failure Reporting

The system SHALL NOT allow a subscription's start-up or background-read failure to go unreported or
crash the application. When starting a subscription's underlying NATS subscription throws, or its
background read loop faults for any reason other than its own cancellation (deletion), the system
SHALL remove that subscription from the active list and report the failure to the user via a
dismissible error dialog. Removal for a reported failure SHALL go through the same removal path used
for a user-initiated delete, so that if the subscription has already been removed by the time the
failure is detected (e.g. the user deleted it concurrently), the failure is not reported.

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

#### Scenario: A cancellation not caused by deletion is reported and removed
- **WHEN** an active subscription's background read loop raises a cancellation that was not caused
  by the user deleting that subscription (e.g. the underlying connection cancels it)
- **THEN** an error dialog is shown describing the failure, and the subscription no longer appears
  in the active list

#### Scenario: A subscription already removed by the time it faults is not reported
- **WHEN** the user deletes an active subscription at the same moment its background read loop is
  independently faulting for an unrelated reason
- **THEN** no failure dialog is shown for that subscription, since it is no longer in the active
  list by the time the fault is detected
