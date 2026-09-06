## 1. StreamListView wiring

- [x] 1.1 Add `Command.DeleteAll` + `Key.D.WithCtrl` binding to `Streams/StreamListView.cs`,
      raising a `DeleteRequested` event (same shape as the existing `Command.New`/Ctrl+N block)
- [x] 1.2 Add the Ctrl+D shortcut to `StreamListView`'s `IShortcutSource.Shortcuts` (append to
      the base `DrillableListView<T>.Shortcuts`, alongside the existing "New" hint) so it
      surfaces in the status bar

## 2. StreamsTab delete handling

- [x] 2.1 In `Streams/StreamsTab.cs`, wire `_listView.DeleteRequested += () => _ =
      TryDeleteStreamAsync()`
- [x] 2.2 Implement `TryDeleteStreamAsync`: no-op if `_listView.SelectedStream` is `null`;
      otherwise confirm via `MessageBox.Query(App!, "Delete Stream", $"Delete stream '{name}'?
      This cannot be undone.", "_Delete", "_Cancel")` with `_Delete` first / `_Cancel` last (so
      Cancel is the Enter-activated default per `doc/terminal-gui-howto.md` gotcha #1); proceed
      only when the result is index `0`
- [x] 2.3 On confirm, `await _jetStream.DeleteStreamAsync(name)`; on success, `_ =
      RefreshListAsync(neighborName)`, where `neighborName` is computed from `_items` before the
      delete call as the stream below the one being deleted, or above it if it was last, so the
      highlight lands near where it was instead of jumping to the top of the list
- [x] 2.4 On `DeleteStreamAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, "Delete Stream Failed", ex.Message, "_Ok")` — body is exactly
      `ex.Message`, no other text; no dialog to reopen/reseed (unlike create's failure path)

## 3. Verification

- [x] 3.1 Manual pass via tmux against a real `nats-server`: highlight a stream, Ctrl+D, confirm
      the prompt names that stream
- [x] 3.2 Manual pass: press Enter on the open prompt without navigating — confirm nothing is
      deleted (Cancel is the default) and the list is unchanged
- [x] 3.3 Manual pass: choose Delete — confirm the stream disappears from the list and (via `nats
      stream ls` or the app itself) no longer exists on the server
- [x] 3.4 Manual pass: choose Cancel, and separately press Esc — confirm neither deletes anything
- [x] 3.5 Verified by code inspection: `TryDeleteStreamAsync` guards on
      `_listView.SelectedStream?.Config.Name is not { } name) return;`, the identical
      null-on-empty-list pattern `Descend()` already relies on in the same file — an empty
      `SelectedItem` never opens the confirm prompt. Not re-verified live since the app's NATS
      connection is hardcoded to `localhost:4222` (`Program.cs`), which already has streams; an
      empty-list live pass would need a second throwaway server instance.
- [x] 3.6 Manual pass: attempt delete against a stream name that no longer exists server-side
      (e.g. deleted concurrently by another client) — confirm `MessageBox.ErrorQuery` shows the
      server's error and the list is left as-is until the next refresh
