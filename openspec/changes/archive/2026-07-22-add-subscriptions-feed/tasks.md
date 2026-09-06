## 1. Message Envelope

- [x] 1.1 Add `FeedEnvelope` record: `(DateTimeOffset ReceivedAt, Guid SubscriptionId, NatsMsg<byte[]> Message)`

## 2. Subscription Registry & Channel Pipeline

- [x] 2.1 Add a subscription registry service: tracks active subject patterns (each with its own assigned `Guid` subscription id), and for each one owns a background task iterating `connection.SubscribeAsync<byte[]>(pattern)`, wrapping each received message in a `FeedEnvelope` (stamping `ReceivedAt` and its `SubscriptionId`), and writing the envelope into a shared `Channel<FeedEnvelope>`
- [x] 2.2 Implement add: assigns a new `Guid` subscription id, starts its background task, and begins writing envelopes into the shared channel
- [x] 2.3 Implement delete: removing a pattern cancels its background task and disposes its subscription, with no further writes from that subscription id into the shared channel
- [x] 2.4 Register the registry and the shared channel as singletons via DI (replacing the `heartbeat` keyed singleton in `Services.cs`/`Program.cs`)

## 3. Duplicate Collapsing

- [x] 3.1 Implement a dedup key function over `FeedEnvelope.Message`: `hash(subject + headers + payload)`, explicitly not including `ReceivedAt` or `SubscriptionId`
- [x] 3.2 Implement a trailing time-window cache of recently-seen keys; when an envelope's key was already seen within the window, suppress it before it reaches the feed view
- [x] 3.3 Choose an initial window size (small, e.g. tens of milliseconds) and note it's a starting guess pending real traffic

## 4. Feed Reader Loop

- [x] 4.1 Implement the reader loop against the shared channel's reader: `await WaitToReadAsync()`, then drain via `TryRead` into a batch of `FeedEnvelope`
- [x] 4.2 Apply dedup (section 3) to each envelope before adding it to the batch
- [x] 4.3 Dispatch each non-empty batch to the UI thread with a single `App.Invoke` call

## 5. Subscriptions Screen

- [x] 5.1 Add a Subscriptions view listing currently active subject patterns
- [x] 5.2 Add UI to add a new pattern (text input dispatching to registry add)
- [x] 5.3 Add UI to delete a selected pattern (dispatching to registry delete)
- [x] 5.4 Wire the Subscriptions view into `MainWindow` alongside the existing feed area

## 6. Feed View Integration

- [x] 6.1 Adapt `LiveLogDataSource`/`LiveUpdatesView` (or their replacement) to render structured `FeedEnvelope` rows (`ReceivedAt` as the timestamp column, subject, headers, payload) instead of flat strings
- [x] 6.2 Wire the feed view to the reader loop's batched output (section 4) instead of the `heartbeat` observable
- [x] 6.3 Remove the `heartbeat` demo wiring from `Program.cs`/`Services.cs`

## 7. Verification

- [x] 7.1 Manually verify: adding a subscription pattern and publishing a matching message shows it in the feed
- [x] 7.2 Manually verify: deleting a subscription stops new matching messages from appearing
- [x] 7.3 Manually verify: two overlapping active patterns matching the same published message produce exactly one feed row
- [x] 7.4 Manually verify: a burst of messages published in quick succession is reflected in the feed without a UI stall (single-batch dispatch working as intended)
