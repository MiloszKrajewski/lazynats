## MODIFIED Requirements

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

#### Scenario: A cancellation not caused by deletion is reported and removed
- **WHEN** an active subscription's background read loop raises a cancellation that was not caused
  by the user deleting that subscription (e.g. the underlying connection cancels it)
- **THEN** an error dialog is shown describing the failure, and the subscription no longer appears
  in the active list
