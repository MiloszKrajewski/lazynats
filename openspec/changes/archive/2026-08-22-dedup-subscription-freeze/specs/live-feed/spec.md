## MODIFIED Requirements

### Requirement: Duplicate Collapsing for Overlapping Subscriptions
The system SHALL compute a dedup key as a 64-bit hash of `subject + headers + payload` from
each envelope's inner `NatsMsg<byte[]>`, deliberately excluding the envelope's receipt
timestamp and subscription identity from the hash itself. For each key, the system SHALL track
the receipt timestamp and `SubscriptionId` of the most recent envelope that was **not**
suppressed as a duplicate for that key (its "canonical" envelope). An incoming envelope SHALL be
suppressed as a duplicate only if a canonical envelope for the same key was recorded within a
short trailing time window and the incoming envelope's `SubscriptionId` differs from the
canonical one's. The system SHALL update the stored canonical timestamp and `SubscriptionId`
only when an envelope is **not** suppressed; a suppressed (duplicate) envelope SHALL leave the
stored canonical state unchanged. This distinguishes a message matching more than one active
overlapping subscription pattern (collapsed to one row) from a genuinely distinct repeat publish
of identical content (not collapsed), including when the two cases occur close together while
the same overlapping subscriptions remain active.

#### Scenario: The same message delivered via two overlapping subscriptions appears once
- **WHEN** subscriptions for `invoices.>` and `invoices.get.*` are both active and a message is published on `invoices.get.123`, causing the server to deliver it once per matching subscription
- **THEN** the feed shows exactly one row for that message

#### Scenario: Receipt timestamp is not part of the dedup key
- **WHEN** the same message is received twice (once per matching subscription) with different `FeedEnvelope.ReceivedAt` values
- **THEN** both receipts still produce the same dedup key and are collapsed to one row

#### Scenario: Subscription identity is excluded from the hash key but used to decide collapsing
- **WHEN** the same message is received via two different active subscriptions, each tagging its envelope with a different `SubscriptionId`
- **THEN** both envelopes still produce the same dedup key, and the second envelope is suppressed because its `SubscriptionId` differs from the first (canonical) envelope's

#### Scenario: A repeat publish on the same subscription is not collapsed
- **WHEN** a subscription's `SubscriptionId` receives two envelopes with identical subject, headers, and payload within the trailing dedup window
- **THEN** both envelopes appear as separate rows in the feed, because the second envelope's `SubscriptionId` matches the stored canonical `SubscriptionId` for that key

#### Scenario: Suppressed duplicates do not change which subscription is canonical
- **WHEN** a key's canonical envelope came from subscription A, and envelopes from other overlapping subscriptions (e.g. B, then C) are subsequently suppressed as duplicates of it
- **THEN** the stored canonical `SubscriptionId` for that key remains A after each suppression, so a later genuine repeat delivered again via subscription A is still recognized as matching the canonical subscription and is not collapsed

#### Scenario: Distinct messages outside the window are not collapsed
- **WHEN** two messages with identical subject, headers, and payload are received with a gap larger than the trailing dedup window
- **THEN** both appear as separate rows in the feed

#### Scenario: Dedup key is 64-bit
- **WHEN** a dedup key is computed for any envelope
- **THEN** the key is a 64-bit value, not the previous 32-bit `HashCode`-derived value
