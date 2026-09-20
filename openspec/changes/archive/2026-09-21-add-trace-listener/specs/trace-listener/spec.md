## ADDED Requirements

### Requirement: Debug-Only Compilation
The system SHALL define `TraceFeedListener` and register it only under `#if DEBUG`, so the type
and its registration are entirely absent from Release/AOT builds.

#### Scenario: Listener type is absent from a Release build
- **WHEN** the app is compiled in a non-DEBUG (Release) configuration
- **THEN** `TraceFeedListener` is not compiled into the resulting assembly and no trace listener
  is registered with `System.Diagnostics.Trace`

#### Scenario: Listener is active in a DEBUG build
- **WHEN** the app is run from a DEBUG build
- **THEN** a `TraceFeedListener` instance is registered with `System.Diagnostics.Trace.Listeners`
  during startup

### Requirement: Trace Calls Become Synthetic Live Feed Envelopes
The system SHALL forward each `System.Diagnostics.Trace.Write`/`Trace.WriteLine` call to the
shared Live Feed sink (`IObserver<FeedEnvelope>`) as a `FeedEnvelope` wrapping a synthetic
`NatsMsg<byte[]>` on subject `$TRACE`, with the call's message UTF-8 encoded as the payload,
without creating, requiring, or going through any NATS subscription or publish.

#### Scenario: A Trace.WriteLine call appears as a Live Feed row
- **WHEN** any code in the app calls `Trace.WriteLine("something")` while `TraceFeedListener` is
  registered
- **THEN** a row appears in the Live Feed with subject `$TRACE` and payload text `"something"`

#### Scenario: A Trace.Write call appears as its own Live Feed row
- **WHEN** any code in the app calls `Trace.Write("something")` while `TraceFeedListener` is
  registered
- **THEN** a row appears in the Live Feed with subject `$TRACE` and payload text `"something"`,
  independent of any other buffered `Write`/`WriteLine` call

#### Scenario: No subscription is created or required
- **WHEN** `TraceFeedListener` pushes an envelope for a `Trace` call
- **THEN** no entry is added to `SubscriptionRegistry`'s active subscriptions, and no NATS
  subscribe or publish call is made as part of producing that envelope

#### Scenario: Null or empty trace messages produce no row
- **WHEN** `Trace.Write` or `Trace.WriteLine` is called with a `null` or empty-string message
- **THEN** no `FeedEnvelope` is pushed to the Live Feed sink for that call

### Requirement: Synthetic Envelopes Share the Real Feed Pipeline
A synthetic `$TRACE` envelope SHALL flow through the same batching, deduplication, and rendering
pipeline as an envelope produced from a real NATS subscription, with no special-cased path.

#### Scenario: A $TRACE row is batched and rendered like any other row
- **WHEN** a `Trace.WriteLine` call produces a synthetic envelope while other real subscription
  envelopes are also arriving
- **THEN** the synthetic envelope is subject to the same batched main-thread dispatch, dedup
  keying, and row formatting as the real envelopes, with no separate rendering path

#### Scenario: Repeated identical trace text is never collapsed by dedup
- **WHEN** two `Trace.WriteLine` calls with identical text occur within the feed's trailing dedup
  window
- **THEN** both appear as separate Live Feed rows, because every synthetic envelope shares the
  same fixed `SubscriptionId` and dedup only suppresses a message whose `SubscriptionId` differs
  from the stored canonical one for that key
