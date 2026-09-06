## 1. Reactive polling infrastructure

- [x] 1.1 Create `src/lazynats/Core/AsyncExtensions.cs` with `SelectAsync<U, V>(this IObservable<U>, Func<U, Task<V>>)` implemented as `Select` into `Observable.FromAsync` + `Concat` (sequential, non-overlapping, order-preserving).
- [x] 1.2 Add `ObserveOnApp<T>(this IObservable<T>, IApplication app)` to the same file, wrapping the observer so every `OnNext`/`OnError`/`OnCompleted` is dispatched via `app.Invoke(...)`.

## 2. Refactor `StreamDetails` onto the new pipeline

- [x] 2.1 Give `StreamDetails` its own `_active` bool field and a `SetActive(bool)` method.
- [x] 2.2 Replace its poll loop with `Observable.Interval(3s).Where(_ => _active).SelectAsync(_ => FetchAsync(_target)).Where(info => info is not null).ObserveOnApp(app).Subscribe(Show)`, with `FetchAsync` catching internally and returning `null` on failure (see design.md decision 3 for why a downstream `Catch` operator was rejected), created once (lazily, first time `App` is available) and never stopped/recreated.
- [x] 2.3 Update `StreamsTab` to call `_details.SetActive(bool)` on tab focus change instead of the current `StartPolling`/`StopPolling`/`App.AddTimeout` pair; remove the now-unused `_pollTarget`/`_pollToken` staleness-guard code (`StreamsTab.cs:21-24`, `74-88`, `103-117`) since `Concat` makes it redundant.
- [x] 2.4 Manually verify (tmux) that the stream detail panel still polls live and stops polling when switching away to another management tab. Verified against the local dockerized `nats` server (`saga-demo` stream): `nats pub saga-demo.events.verify.tick1 ...` while the stream was highlighted on the Streams tab bumped Messages 0→1 within one poll cycle; switched to Subscribe (Alt+1), published a second message (`tick2`), waited past a poll interval — no status-bar error; switched back to Streams (Alt+3) and Messages showed the stale value (1) until the next tick, then updated to 2, confirming polling paused while inactive and resumed correctly on refocus.

## 3. Consumer-level components

- [x] 3.1 Create `src/lazynats/Streams/ConsumerNamePresenter.cs` (mirrors `StreamNamePresenter`, formats `ConsumerInfo.Name`).
- [x] 3.2 Create `src/lazynats/Streams/ConsumerListView.cs` (mirrors `StreamListView`: `PresenterListDataSource<ConsumerInfo>`, empty-hint, `Ctrl+R` → `RefreshRequested`, `HighlightChanged`), adding a custom `AddCommand`/`KeyBindings.Add` for `Key.Esc` and `Key.Backspace` raising an `AscendRequested` event.
- [x] 3.3 Create `src/lazynats/Streams/ConsumerDetails.cs` (mirrors the refactored `StreamDetails` from section 2: same `SetActive`/pipeline shape, `GetConsumerAsync(stream, consumer)` fetch), rendering the fields from `design.md` decision 9 (Name, Filter Subject, Ack Policy, Deliver Policy, Max Deliver, Max Ack Pending / Delivered, Ack Floor, Ack Pending, Redelivered, Waiting, Pending).

## 4. `StreamsTab` navigation wiring

- [x] 4.1 Add level state to `StreamsTab` (stream level vs. consumer level, plus the current stream name when drilled in).
- [x] 4.2 Add a second `EditFrame`-wrapped `ConsumerListView` and a `ConsumerDetails` pane, laid out at the same position as the stream-level pair, toggled via `Visible`.
- [x] 4.3 Wire `StreamListView`'s `Accepted` event to descend: fetch consumers for the highlighted stream, show the consumer-level pair, hide the stream-level pair, `SetActive(false)` on `StreamDetails`, `SetActive(true)` on `ConsumerDetails`. (Added a `DescendRequested` event to `StreamListView` re-raising its inner `ListView.Accepted`, since the inner `ListView` is private.)
- [x] 4.4 Wire `ConsumerListView.AscendRequested` to climb back up: show the stream-level pair, hide the consumer-level pair, `SetActive(false)` on `ConsumerDetails`, `SetActive(true)` on `StreamDetails` — without re-fetching the stream list.
- [x] 4.5 Wire `ConsumerListView.RefreshRequested` (`Ctrl+R`) to re-fetch the current stream's consumer list, preserving highlight per the existing `ReplaceItems` semantics.
- [x] 4.6 Update the breadcrumb `Label`s to switch between `"Streams"`/`"Details"` and `"Consumers of <name>"`/`"Consumer Details"` on descend/ascend.
- [x] 4.7 Update `StreamsTab.OnHasFocusChanged` to call `SetActive(bool)` on whichever pane (`StreamDetails` or `ConsumerDetails`) is current, instead of the removed centralized timer.

## 5. Verification

- [x] 5.1 `dotnet build src/lazynats.sln` compiles clean.
- [x] 5.2 tmux-drive the app against a real NATS server with at least one stream that has 2+ consumers: verify Enter descends, breadcrumb updates, consumer list populates, consumer detail polls live, Esc and Backspace both ascend without re-fetching the stream list, `Ctrl+R` at consumer level re-fetches. Verified against the local dockerized `nats` server (`saga-demo` stream, `default`/`default-sagademo` consumers) — all behaviors confirmed via tmux capture-pane.
- [x] 5.3 Verify switching to another management tab (e.g. Alt+1) while drilled into a consumer stops polling, and switching back resumes it targeting the same consumer. Confirmed: no status-bar error after waiting past a poll interval on another tab, and consumer-level state/highlight was intact on switching back.
- [x] 5.4 `openspec validate add-consumer-drilldown --strict` passes.
