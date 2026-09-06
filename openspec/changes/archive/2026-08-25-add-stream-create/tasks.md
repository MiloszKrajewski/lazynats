## 1. NewStreamOptions and field parsing helpers

- [x] 1.1 Create `Streams/NewStreamOptions.cs`: `internal sealed record NewStreamOptions(string
      Name, IReadOnlyList<string> Subjects, StreamConfigRetention Retention, TimeSpan? MaxAge)`
- [x] 1.2 Add `ToStreamConfig()` on/for `NewStreamOptions` (same file) that maps to
      `NATS.Client.JetStream.Models.StreamConfig`: `Name`, `Subjects`, `Retention` pass straight
      through; `MaxAge` maps `null -> TimeSpan.Zero`; plus the explicit safe defaults from
      design.md — `MaxMsgs = -1`, `MaxBytes = -1`, `MaxConsumers = -1`, `MaxMsgSize = -1`,
      `MaxMsgsPerSubject = -1`, `NumReplicas = 1` — with a comment explaining why (CLR default `0`
      for these fields is not the same as "unlimited")
- [x] 1.3 Add `ParseSubjects(string) -> IReadOnlyList<string>`, splitting on space/comma/
      semicolon, trimming, and dropping empty tokens
- [x] 1.4 Add `TryParseMaxAge(string text, out TimeSpan? result) -> bool`: empty/whitespace input
      returns `true` with `result = null`; non-empty input returns `true` with the parsed
      `TimeSpan` on success or `false` (unparseable) otherwise

## 2. CreateStreamDialog

- [x] 2.1 Create `Streams/CreateStreamDialog.cs` as `Dialog<NewStreamOptions>` with labeled
      fields: Name (`TextField`), Subjects (`TextField`), Retention
      (`DropDownList<StreamConfigRetention>`, defaulted to `Limits`), Max Age (`TextField`);
      constructor takes an optional `NewStreamOptions? initial = null` to pre-fill all fields
      (used on retry after a failed create)
- [x] 2.2 Wire per-field validity (reuse `PatternDialog`'s red-text-on-`EditableBackground`
      convention) for Name (non-empty), Subjects (`ParseSubjects` yields ≥1 item), Max Age
      (`TryParseMaxAge` returns `true`)
- [x] 2.3 Add a single `Create` button, disabled/inert while any field is invalid; no separate
      `Cancel` button (Esc already cancels via `Dialog<T>`'s built-in behavior); override
      `OnAccepting` to swallow Enter pressed on a field (a no-op, not a submit or a cancel) so Tab
      stays the only way to move between fields
- [x] 2.4 On `Create`, build `Result` as a `NewStreamOptions`: `Name`, `Subjects` from
      `ParseSubjects`, `Retention` from the dropdown's `.Value`, `MaxAge` from the `out` value of
      `TryParseMaxAge` — the dialog never constructs or references `StreamConfig` directly
- [x] 2.5 Set `Result` and `RequestStop()` on `Create`; leave `Result` unset on Esc

## 3. Wiring into the stream list and tab

- [x] 3.1 Add `Command.New` + `Key.N.WithCtrl` binding to `Streams/StreamListView.cs` (mirroring
      how `ConsumerListView` layers its own Esc/Backspace on `DrillableListView<T>`), raising a
      `CreateRequested` event
- [x] 3.2 In `Streams/StreamsTab.cs`, handle `CreateRequested` with a retry loop: run
      `CreateStreamDialog` (seeded from the prior attempt's values after a failure, `null`
      initially); if the user cancels (`Result is null`), stop; otherwise try
      `_jetStream.CreateStreamAsync(result.ToStreamConfig())`
- [x] 3.3 On success, re-run `RefreshListAsync()` so the new stream appears and is highlighted
      via `ReplaceItems`'s existing identity-preserving logic, then stop the loop
- [x] 3.4 On `CreateStreamAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, "Create Stream Failed", ex.Message, "_Ok")` — body is exactly
      `ex.Message`, no other text — then loop back to 3.2, reopening `CreateStreamDialog` seeded
      with the just-entered `NewStreamOptions`
- [x] 3.5 Add the Ctrl+N shortcut to `StreamListView`'s `IShortcutSource.Shortcuts` (append to
      the base `DrillableListView<T>.Shortcuts`, same pattern `ConsumerListView` would use for
      its own additions) so it surfaces in the status bar

## 4. Verification

- [x] 4.1 Manual pass via tmux against a real `nats-server`: Ctrl+N → fill valid fields → Create
      → confirm the stream appears highlighted in the list and its detail panel shows the
      entered Subjects/Retention/Max Age
- [x] 4.2 Manual pass: leave Name/Subjects empty, enter garbage Max Age text — confirm Create
      stays unavailable and invalid fields are flagged
- [x] 4.3 Manual pass: create with empty Max Age — confirm the detail panel shows no max age
      limit
- [x] 4.4 Manual pass: attempt to create a stream with a name that already exists — confirm a
      `MessageBox.ErrorQuery` shows the server's error message and, after dismissing it, the
      create-stream dialog reopens with the previously entered values still filled in
- [x] 4.5 Confirm via `nats stream info <name>` (or the detail panel) that `max_msgs`/`max_bytes`
      came through as `-1` (unlimited) and `num_replicas` as `1`, not `0`
