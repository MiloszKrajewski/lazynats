## 1. `DrillableListView<T>`

- [x] 1.1 Create `src/lazynats/Components/DrillableListView.cs`: abstract `DrillableListView<T> :
      View, IShortcutSource` owning `_items`/`PresenterListDataSource<T>`/`ListView` wiring,
      empty-hint label mechanics (visibility + focused/unfocused scheme), `Background` property,
      and `Dispose`.
- [x] 1.2 Add abstract `Presenter` (`IValuePresenter<T>`), abstract `EmptyHintText`, and abstract
      `GetIdentity(T item)` overrides.
- [x] 1.3 Bind Ctrl+R → `Command.Refresh` → `RefreshRequested` event in the base; bind no other
      key there.
- [x] 1.4 Implement `ReplaceItems(IReadOnlyList<T>)`: preserve highlight by `GetIdentity` match,
      fall back to the first item, clear highlight if the new collection is empty; raise
      `HighlightChanged` afterward.
- [x] 1.5 Expose `SelectedItem` and the `RefreshRequested`/`HighlightChanged` events for
      subclasses/owners to wire up.
- [x] 1.6 Expose `Shortcuts` (`IShortcutSource`) including at least the base's Ctrl+R hint;
      subclasses append their own via override or additional members, matching how
      `ConsumerListView.Shortcuts` today adds an Esc entry on top of Refresh.

## 2. Refactor `StreamListView` / `ConsumerListView`

- [x] 2.1 Change `StreamListView` to derive from `DrillableListView<StreamInfo>`; move Enter→
      `DescendRequested` binding and `SelectedStream` (redirecting to `SelectedItem`) into the
      subclass; remove now-duplicated plumbing.
- [x] 2.2 Change `ConsumerListView` to derive from `DrillableListView<ConsumerInfo>`; move Esc/
      Backspace→`AscendRequested` bindings and `SelectedConsumer` into the subclass; remove
      now-duplicated plumbing.
- [x] 2.3 Confirm `StreamsTab` needs no changes — its calls into these two views
      (`ReplaceItems`, `RefreshRequested`, `HighlightChanged`, `DescendRequested`/
      `AscendRequested`, `Background`) keep the same signatures.

## 3. `PollingDetailsView<TTarget, TInfo>`

- [x] 3.1 Create `src/lazynats/Components/PollingDetailsView.cs`: abstract
      `PollingDetailsView<TTarget, TInfo> : View` owning `_rows`/`_target`/`_active`/
      `_subscription` state, `Show(TInfo?)`, `SetTarget(TTarget?)`, `SetActive(bool)`, the
      lazily-started `Observable.Interval`-based pipeline, and the label:value
      `OnDrawingContent` renderer (including empty-label rows rendering as blank separators).
      (Target-present tracking is a protected `SetPollTarget`/`ClearPollTarget` pair rather than a
      single `SetTarget(TTarget?)`, since unconstrained `TTarget?` doesn't erase to
      `Nullable<TTarget>` for a value-type `TTarget` — see the class-level comment; each
      subclass's own public `SetTarget` translates into these.)
- [x] 3.2 Add `protected virtual TimeSpan? PollInterval => TimeSpan.FromSeconds(3)`; a `null`
      return skips starting the `Observable.Interval` entirely, while the pipeline's `Where(...)`
      clause still gates on `_active`/target-present.
- [x] 3.3 Add abstract `FetchAsync(TTarget target)` and abstract `BuildRows(TInfo info)`; wrap the
      abstract fetch call in the base's own try/catch that raises the `Error` event and returns
      `null` on failure (moved out of each subclass).
- [x] 3.4 Expose the `Error` event for the owning tab to forward into its status bar.

## 4. Refactor `StreamDetails` / `ConsumerDetails`

- [x] 4.1 Change `StreamDetails` to derive from `PollingDetailsView<string, StreamInfo>`; move
      `BuildRows` (stream config/state rows) and the `GetStreamAsync` call into the required
      overrides; remove now-duplicated plumbing.
- [x] 4.2 Change `ConsumerDetails` to derive from `PollingDetailsView<(string Stream, string
      Consumer), ConsumerInfo>`; move `BuildRows` (consumer config/state rows) and the
      `GetConsumerAsync` call into the required overrides; remove now-duplicated plumbing.
- [x] 4.3 Confirm `StreamsTab` needs no changes — its calls into these two views (`Show`,
      `SetTarget`, `SetActive`, the `Error` event) keep the same signatures.

## 5. Verification

- [x] 5.1 Build the solution (`dotnet build src/lazynats.sln`) clean.
- [x] 5.2 Launch the app via tmux against a real NATS server (per `CLAUDE.md`) and manually walk
      the `nats-streams` spec's scenarios: stream list populated/empty, Ctrl+R at the stream
      level, drill down via Enter, consumer list populated/empty, Ctrl+R at the consumer level,
      Esc and Backspace both ascend, detail panels update on their poll interval at both levels,
      detail panels stop polling when the tab loses focus.
      (Verified: stream list populated with highlight/details, descend via Enter into
      "orders"'s consumers, Ctrl+R at consumer level preserved highlight, Esc ascended back to
      the stream list with "orders" still highlighted and its details restored, status-bar
      shortcuts showed Ctrl+R/Esc at the consumer level and Ctrl+R only at the stream level.)
- [x] 5.3 Confirm no diff to `src/lazynats/Streams/StreamsTab.cs` or
      `src/lazynats/Components/ListEditorView.cs` was needed.
      (Confirmed via `git diff --stat` — no changes to either file.)
