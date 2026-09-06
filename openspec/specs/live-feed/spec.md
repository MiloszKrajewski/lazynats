# live-feed Specification

## Purpose
Provide a single global, deduplicated, low-overhead live feed of messages merged across all active NATS subscriptions, independent of which subscription produced a given message.

## Requirements

### Requirement: Message Envelope
The system SHALL wrap each received `NatsMsg<byte[]>` in a `FeedEnvelope` carrying the client-side receipt timestamp and the identity of the subscription that produced it, before the message enters the shared feed pipeline, so that receipt time and provenance are available to every downstream consumer (dedup, rendering) without being recomputed or inferred later.

#### Scenario: Every message entering the feed pipeline carries receipt time and subscription identity
- **WHEN** a message is received from an active subscription
- **THEN** it is wrapped in a `FeedEnvelope` with a receipt timestamp and that subscription's identity before being written to the shared channel

### Requirement: Global Merged Feed Channel
The system SHALL merge messages from all active subscriptions into a single shared `Channel<FeedEnvelope>`, with one background reader task per active subscription writing into it, so the feed is a single global stream independent of how many subscriptions are active or which one produced a given message.

#### Scenario: Messages from multiple active subscriptions share one feed
- **WHEN** two different subject-pattern subscriptions are both active and each receives a message
- **THEN** both messages appear in the same global feed, in the order they were written to the shared channel

#### Scenario: Feed is not scoped to a selected subscription
- **WHEN** the user selects a specific subscription in the Subscriptions screen
- **THEN** the feed continues to show messages from all active subscriptions, not only the selected one

### Requirement: Batched Main-Thread Dispatch
The system SHALL buffer messages from the shared feed for a fixed 25ms window - matching the
app's own render cadence closely enough to add no perceptible latency while avoiding
clock-phase-drift noise against it - and SHALL dispatch each non-empty window's messages to the
UI thread with a single call, so that UI-thread marshaling overhead does not scale with
per-message throughput.

#### Scenario: A burst of messages is dispatched as one batch
- **WHEN** multiple messages are published to the shared feed within the same 25ms window
- **THEN** all of them are dispatched together and applied to the feed view via a single
  UI-thread call, not one dispatch per message

#### Scenario: A window with no messages produces no dispatch
- **WHEN** a 25ms window elapses with no messages published to the shared feed
- **THEN** no UI-thread dispatch call occurs for that window

#### Scenario: Windows with pending dispatches are all applied on the next main-loop iteration
- **WHEN** the UI thread is busy long enough for more than one window's dispatch to become queued
- **THEN** all queued dispatches are applied on the next main-loop iteration before it redraws,
  none are dropped, and none wait for a further iteration

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

### Requirement: No In-View Header
The live feed view SHALL NOT render its own heading text or divider line; it SHALL rely on its
host container's own title and border for framing, so that the view never duplicates a title
already shown by whatever it is hosted in.

#### Scenario: Feed view renders without a redundant heading
- **WHEN** the live feed view is displayed inside its host frame (titled "Live Feed")
- **THEN** the view shows no additional heading text or divider line of its own, and its message
  list starts at the top row of the view's content area
