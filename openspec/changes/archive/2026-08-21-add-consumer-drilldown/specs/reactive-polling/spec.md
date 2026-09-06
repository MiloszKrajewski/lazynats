## ADDED Requirements

### Requirement: Sequential Async Projection
The system SHALL provide an observable operator (`SelectAsync`) that projects each element of a
source sequence through an asynchronous operation, running at most one such operation at a time
and never dropping a source element, even if a new source element arrives before the previous
operation completes.

#### Scenario: A second element arrives while the first's operation is still running
- **WHEN** a source sequence produces an element while the asynchronous operation for a prior
  element has not yet completed
- **THEN** the operation for the new element does not start until the prior operation completes

#### Scenario: No source element's operation is skipped
- **WHEN** a source sequence produces several elements in quick succession
- **THEN** every element's asynchronous operation eventually runs, in the same order the elements
  arrived

### Requirement: UI-Thread Marshaling for Observables
The system SHALL provide an observable operator (`ObserveOnApp`) that dispatches every
notification (value, error, and completion) of a source sequence onto the application's UI-thread
event loop before it reaches downstream subscribers, so subscriber code can safely touch UI state
directly.

#### Scenario: Notifications are delivered on the UI thread
- **WHEN** a source sequence produces a notification from a background thread
- **THEN** the subscriber receives that notification on the application's UI thread

### Requirement: Active-Gated Polling
A component that polls on a fixed interval using this infrastructure SHALL make no outbound call
for a tick while it is marked inactive, and the underlying timer SHALL continue running
regardless of the active state (it is only ever restarted on component disposal, never on
activation changes).

#### Scenario: An inactive poller issues no calls
- **WHEN** a poll interval elapses while the component is marked inactive
- **THEN** no outbound call is made for that tick

#### Scenario: Reactivating resumes calls without restarting the timer
- **WHEN** a component transitions from inactive to active
- **THEN** the next elapsed poll interval results in an outbound call, without the timer having
  been stopped or recreated

#### Scenario: A failed call is reported, not thrown
- **WHEN** an active poller's outbound call fails
- **THEN** the failure is reported through the component's normal error-reporting path and the
  poll pipeline continues running on its next interval, rather than terminating the application
