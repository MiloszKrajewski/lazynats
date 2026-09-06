## 1. Shared subject wiring

- [x] 1.1 In `Program.cs`, replace `Channel.CreateUnbounded<FeedEnvelope>()` and its
      `channel.Reader`/pass-to-registry wiring with a single
      `Subject<FeedEnvelope>().Synchronize()` instance, registered in the DI container as
      `IObserver<FeedEnvelope>` (for `SubscriptionRegistry`) and `IObservable<FeedEnvelope>`
      (for `LiveUpdatesView`) - same instance, two narrowed registrations. Do not register the
      concrete `Subject<FeedEnvelope>` type itself.
- [x] 1.2 Remove the now-unused `Channel<FeedEnvelope>`/`ChannelReader<FeedEnvelope>` DI
      registrations and `using System.Threading.Channels;` if no longer needed in `Program.cs`.

## 2. Producer side

- [x] 2.1 In `Subscriptions/SubscriptionRegistry.cs`, change the constructor parameter from
      `ChannelWriter<FeedEnvelope> writer` to `IObserver<FeedEnvelope> sink`.
- [x] 2.2 In `RunAsync`, replace `await _writer.WriteAsync(envelope, cancellationToken)` with
      `_sink.OnNext(envelope)` (synchronous - no `await` needed for this line).

## 3. Consumer side

- [x] 3.1 In `LiveUpdatesView.cs`, change the constructor parameter from
      `ChannelReader<FeedEnvelope> feed` to `IObservable<FeedEnvelope> feed`.
- [x] 3.2 Replace the `FeedReaderLoop`/`_cts` construction with the Rx chain: `feed.Where(e =>
      !dedup.IsDuplicate(e)).Buffer(TimeSpan.FromMilliseconds(25)).Where(batch => batch.Count >
      0).ObserveOnApp(App!).Subscribe(batch => { foreach (var envelope in batch) OnEvent(envelope);
      })`, storing the returned `IDisposable` in a `_subscription` field.
- [x] 3.3 Replace `_cts` field and its `Cancel()`/`Dispose()` calls in `Dispose(bool disposing)`
      with `_subscription.Dispose()`.
- [x] 3.4 Remove the now-unused `OnBatch` method and `using System.Threading.Channels;`.

## 4. Cleanup

- [x] 4.1 Delete `LiveFeed/FeedReaderLoop.cs`.
- [x] 4.2 Update `LiveFeed/MessageDeduplicator.cs`'s class comment: replace the reference to
      "the single `FeedReaderLoop` that owns it" with a reference to being called from the
      synchronized subject's `Where` (i.e. `Subject.Synchronize()` guarantees single-caller
      access, same invariant, different owner).
- [x] 4.3 In `MainWindow.cs`, change `Services.Root.GetRequiredService<ChannelReader<FeedEnvelope>>()`
      to `Services.Root.GetRequiredService<IObservable<FeedEnvelope>>()` and update the
      `LiveUpdatesView` construction call accordingly.

## 5. Verification

- [x] 5.1 `dotnet build src/lazynats.sln` succeeds with no warnings from the changed files.
- [x] 5.2 Manually verify (via `tmux`, per `CLAUDE.md`'s testing guidance, against a real
      `nats-server`) that: a single active subscription's messages still appear in the live
      feed; two overlapping subscriptions matching the same message still collapse to one row;
      a burst of messages still renders promptly without visible per-message lag.
      Found and fixed a real bug in the process: wiring the Rx chain directly in
      `LiveUpdatesView`'s constructor (as tasks 3.2 originally specified) crashed with a
      `NullReferenceException` on the first message, because `View.App` resolves via the
      SuperView chain and is still null at construction time (this view isn't added to
      `feedFrame`/`MainWindow` yet) - the design's assumption that `App` was already live by
      then (based on `MainWindow.cs` using `App!` in its own constructor) was wrong, since that
      usage is inside a deferred event-handler lambda, not executed at construction time. Fixed
      by deferring the subscription to the view's `Initialized` event instead. Verified: single
      subscription renders messages; two overlapping subscriptions (`invoices.>` and
      `invoices.get.*`) matching the same message collapse to one row; a 20-message burst
      renders without crashing or visible lag.
- [x] 5.3 Run `src/lazynats.AotProbe`'s `test-aot.ps1` to confirm the changed dependency usage
      still publishes and runs cleanly under `PublishAot`.
      Extended `RxProbe.cs` to actually exercise `Subject.Synchronize` and `.Buffer(TimeSpan)`
      (previously it only covered a bare `Subject`/`Subscribe`, neither of the two APIs this
      change introduces). First attempt was blocked by a missing `vswhere.exe` on `PATH`
      (pre-existing machine toolchain gap, unrelated to this change) - resolved after `vswhere`
      was installed. Re-ran: `dotnet publish -r win-x64 --self-contained -p:PublishAot=true`
      succeeds, and all 5 probes (`Nats`, `Rx`, `Di`, `Editor`, `XxHash3`) pass against the
      dockerized `nats-server`, confirming `Subject.Synchronize`/`Buffer(TimeSpan)` trim/AOT
      cleanly.
