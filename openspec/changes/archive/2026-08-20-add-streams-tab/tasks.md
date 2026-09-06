## 1. JetStream wiring

- [x] 1.1 In `Program.cs`, create an `INatsJSContext` from the existing `connection`
      (`connection.CreateJetStreamContext()`) and register it as a singleton via
      `services.AddSingleton(...)`, alongside the existing `NatsConnection`/`registry`
      registrations.

## 2. `StreamListView`

- [x] 2.1 Create `src/lazynats/Streams/StreamListView.cs`: a plain `ListView`-backed component
      (not a `ListEditorView<T>` subclass) presenting an `IReadOnlyList<StreamInfo>` (or
      equivalent) via row text produced by an `IValuePresenter<StreamInfo>` (stream name), reusing
      `PresenterListDataSource` for the data source, matching `nats-streams`'s "Stream List"
      requirement.
- [x] 2.2 Implement the empty-state hint (non-interactive line, dim/focused styling) shown when
      the backing collection is empty, matching `nats-streams`'s "no streams exist" scenario and
      `ListEditorView`'s existing visual convention (independently implemented, not inherited).
- [x] 2.3 Implement Ctrl+R: re-run `ListStreamsAsync`, replace the list contents, and preserve the
      current highlight if that stream is still present, otherwise select the first item — per
      the "Manual List Refresh" requirement. Advertise it via `IShortcutSource` so it surfaces in
      the status bar like other discoverable shortcuts.
- [x] 2.4 Confirm no create/edit/delete keybinding exists on this view, per the "Read-Only List"
      requirement, and that the list does not change on its own between Ctrl+R presses.

## 3. `StreamDetails`

- [x] 3.1 Create `src/lazynats/Streams/StreamDetails.cs`: a read-only panel rendering a single
      `StreamInfo`'s config (`Subjects`, `Retention`, `MaxMsgs`, `MaxBytes`, `MaxAge`,
      `NumReplicas`) and state (`Messages`, `Bytes`, `FirstSeq`, `LastSeq`, `ConsumerCount`).
- [x] 3.2 Handle the "no stream highlighted" case (empty list) by showing no stream's details
      rather than stale or placeholder content.

## 4. `StreamsTab` composition and detail polling

- [x] 4.1 Create `src/lazynats/Streams/StreamsTab.cs` taking `INatsJSContext` in its
      constructor (mirroring how `SubscribeTab`/`PublishTab` take their dependencies), composing
      `StreamListView` (left) and `StreamDetails` (right), self-contained per
      `tab-content-structure` (no layout assembly expected from `MainWindow`).
- [x] 4.2 Populate `StreamListView` once via `ListStreamsAsync` when the tab is first entered
      (not on a timer).
- [x] 4.3 Implement the detail poll: on a 3-second timer, call `GetStreamAsync` for only the
      currently-highlighted stream and update `StreamDetails` from the result — the list is
      untouched by this poll, per design.md's "list is load-once + manual refresh; only the
      highlighted stream's detail polls" decision.
- [x] 4.4 Start the detail poll only while the Streams tab is selected; stop it when the tab is
      deselected or the view is disposed, per the "Refresh does not run while the tab is not
      selected" scenario. Re-target the poll to the new highlight whenever the selected stream
      changes.
- [x] 4.5 On a poll or refresh error (server unreachable, JetStream disabled), keep the previous
      displayed content and surface the error inline rather than clearing the view.

## 5. Integration

- [x] 5.1 In `MainWindow.cs`, resolve `INatsJSContext`, construct `StreamsTab` with title
      `" 3:Streams "` (matching the existing `" 1:Subscribe "`/`" 2:Publish "` spacing/title
      convention) and appropriate `Padding.Thickness`, and register it with `ManagementTabs`
      alongside the existing tabs.
- [x] 5.2 Verify Alt+3 switches to the Streams tab from anywhere in the app, and that its title
      renders as `3:Streams`, per the `tab-navigation` delta.

## 6. Verification

- [x] 6.1 Against a local `nats-server` with JetStream enabled and at least one stream created
      (e.g. via `nats stream add`), drive the app via `tmux` (per `CLAUDE.md`'s TUI-testing
      guidance) to confirm: the stream list populates, highlighting a stream shows its details,
      details update after an external change (e.g. `nats pub` to a stream subject) within one
      poll cycle, the list does *not* change on its own when a stream is added/deleted
      externally, Ctrl+R picks up that external add/delete, and Alt+3 reaches the tab from
      another tab's content.
- [x] 6.2 Confirm the empty-state hint renders correctly when no streams exist (e.g. against a
      fresh JetStream with no streams, or JetStream disabled).
