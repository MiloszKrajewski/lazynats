## MODIFIED Requirements

### Requirement: Stream Detail Panel
The system SHALL show, alongside the stream list, a detail panel for the currently highlighted
stream, presenting at least its configuration (subjects, retention policy, limits, replica count)
and current state (message count, byte size, first/last sequence number, consumer count). Limit
fields that carry a server "no limit" sentinel value SHALL render as `(unlimited)` rather than
their raw sentinel.

#### Scenario: Highlighting a stream shows its details
- **WHEN** the user moves the highlight to a stream in the list
- **THEN** the detail panel shows that stream's configuration and current state

#### Scenario: No stream highlighted
- **WHEN** the stream list is empty and no stream is highlighted
- **THEN** the detail panel shows no stream's details

#### Scenario: Unlimited Max Messages renders as unlimited
- **WHEN** the highlighted stream's `MaxMsgs` is `-1` (the server's "no limit" sentinel)
- **THEN** the Max Messages row shows `(unlimited)` rather than `-1`

#### Scenario: Unlimited Max Bytes renders as unlimited
- **WHEN** the highlighted stream's `MaxBytes` is `-1` (the server's "no limit" sentinel)
- **THEN** the Max Bytes row shows `(unlimited)` rather than `-1`

#### Scenario: Unlimited Max Age renders as unlimited
- **WHEN** the highlighted stream's `MaxAge` is zero (the server's "no limit" sentinel)
- **THEN** the Max Age row shows `(unlimited)` rather than `00:00:00`

#### Scenario: A configured limit still renders as its value
- **WHEN** the highlighted stream's Max Messages, Max Bytes, or Max Age is set to an actual
  positive limit rather than the unlimited sentinel
- **THEN** that row shows the configured value, unchanged from today's rendering

### Requirement: Consumer Detail Panel
The system SHALL show, alongside the consumer list, a detail panel for the currently highlighted
consumer, presenting at least its configuration (filter subject, ack policy, deliver policy, max
deliver, max ack pending) and current state (delivered sequence, ack floor, ack-pending count,
redelivered count, waiting count, pending count). Limit fields that carry a server "no limit"
sentinel value SHALL render as `(unlimited)` rather than their raw sentinel.

#### Scenario: Highlighting a consumer shows its details
- **WHEN** the user moves the highlight to a consumer in the consumer list
- **THEN** the detail panel shows that consumer's configuration and current state

#### Scenario: No consumer highlighted
- **WHEN** the consumer list is empty and no consumer is highlighted
- **THEN** the detail panel shows no consumer's details

#### Scenario: Unlimited Max Deliver renders as unlimited
- **WHEN** the highlighted consumer's `MaxDeliver` is `-1` (the server's "no limit" sentinel)
- **THEN** the Max Deliver row shows `(unlimited)` rather than `-1`

#### Scenario: Unlimited Max Ack Pending renders as unlimited
- **WHEN** the highlighted consumer's `MaxAckPending` is `-1` (the server's "no limit" sentinel)
- **THEN** the Max Ack Pending row shows `(unlimited)` rather than `-1`

#### Scenario: A configured limit still renders as its value
- **WHEN** the highlighted consumer's Max Deliver or Max Ack Pending is set to an actual positive
  limit rather than the unlimited sentinel
- **THEN** that row shows the configured value, unchanged from today's rendering
