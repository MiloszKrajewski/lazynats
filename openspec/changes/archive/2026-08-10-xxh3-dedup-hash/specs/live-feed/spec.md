## MODIFIED Requirements

### Requirement: Duplicate Collapsing for Overlapping Subscriptions
The system SHALL compute a dedup key as a 64-bit hash of `subject + headers + payload` from
each envelope's inner `NatsMsg<byte[]>`, deliberately excluding the envelope's receipt
timestamp and subscription identity, and SHALL suppress a message from being added to the feed
again if the same key was already seen within a short trailing time window, so that a message
matching more than one active overlapping subscription pattern does not appear as multiple
rows.

#### Scenario: The same message delivered via two overlapping subscriptions appears once
- **WHEN** subscriptions for `invoices.>` and `invoices.get.*` are both active and a message is published on `invoices.get.123`, causing the server to deliver it once per matching subscription
- **THEN** the feed shows exactly one row for that message

#### Scenario: Receipt timestamp is not part of the dedup key
- **WHEN** the same message is received twice (once per matching subscription) with different `FeedEnvelope.ReceivedAt` values
- **THEN** both receipts still produce the same dedup key and are collapsed to one row

#### Scenario: Subscription identity is not part of the dedup key
- **WHEN** the same message is received via two different active subscriptions, each tagging its envelope with a different `SubscriptionId`
- **THEN** both envelopes still produce the same dedup key and are collapsed to one row

#### Scenario: Distinct messages outside the window are not collapsed
- **WHEN** two messages with identical subject, headers, and payload are received with a gap larger than the trailing dedup window
- **THEN** both appear as separate rows in the feed

#### Scenario: Dedup key is 64-bit
- **WHEN** a dedup key is computed for any envelope
- **THEN** the key is a 64-bit value, not the previous 32-bit `HashCode`-derived value
