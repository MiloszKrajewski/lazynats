## 1. ConsumerListView wiring

- [x] 1.1 Add `Command.DeleteAll` + `Key.D.WithCtrl` binding to `Streams/ConsumerListView.cs`,
      raising a `DeleteRequested` event (same shape as `StreamListView`'s existing
      `Command.DeleteAll`/Ctrl+D block, and as this file's own existing `Command.New`/Ctrl+N)
- [x] 1.2 Add the Ctrl+D shortcut to `ConsumerListView`'s `IShortcutSource.Shortcuts` (append to
      the base `DrillableListView<T>.Shortcuts`, alongside the existing "Back"/"New" hints) so it
      surfaces in the status bar

## 2. StreamsTab delete handling

- [x] 2.1 In `Streams/StreamsTab.cs`, wire `_consumerListView.DeleteRequested += () => _ =
      TryDeleteConsumerAsync()`
- [x] 2.2 Implement `TryDeleteConsumerAsync`: no-op if `_currentStream` is `null` or
      `_consumerListView.SelectedConsumer` is `null`; otherwise confirm via
      `MessageBox.Query(App!, "Delete Consumer", $"Delete consumer '{name}'? This cannot be
      undone.", "_Delete", "_Cancel")` with `_Delete` first / `_Cancel` last (Cancel is the
      Enter-activated default per `doc/terminal-gui-howto.md` gotcha #1); proceed only when the
      result is index `0`
- [x] 2.3 On confirm, `await _jetStream.DeleteConsumerAsync(stream, name)`; on success, `_ =
      RefreshConsumerListAsync(neighborName)`, where `neighborName` is computed from
      `_consumerItems` before the delete call (below the deleted consumer, or above it if it was
      last) via a new `NeighborConsumerName` helper mirroring `NeighborStreamName`
- [x] 2.4 On `DeleteConsumerAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, "Delete Consumer Failed", ex.Message, "_Ok")` — body is exactly
      `ex.Message`, no other text; no dialog to reopen/reseed

## 3. Verification

- [x] 3.1 Manual pass via tmux against a real `nats-server`: descend into a stream with at least
      two consumers, highlight one, Ctrl+D, confirm the prompt names that consumer
- [x] 3.2 Manual pass: press Enter on the open prompt without navigating — confirm nothing is
      deleted (Cancel is the default) and the consumer list is unchanged
- [x] 3.3 Manual pass: choose Delete — confirm the consumer disappears from the list and (via
      `nats consumer ls <stream>` or the app itself) no longer exists on the server, and that
      highlight lands on the expected neighbor
- [x] 3.4 Manual pass: choose Cancel, and separately press Esc — confirm neither deletes anything
- [x] 3.5 Manual pass: at the stream level (not drilled into a stream), press Ctrl+D — confirm it
      has no effect on any consumer (stream-level Ctrl+D still targets `TryDeleteStreamAsync` as
      before)
- [x] 3.6 Manual pass: attempt delete against a consumer that no longer exists server-side (e.g.
      deleted concurrently by another client) — confirm `MessageBox.ErrorQuery` shows the server's
      error and the list is left as-is until the next refresh
- [x] 3.7 Manual pass: delete the only consumer in the list — confirm the list ends up empty and
      shows `ConsumerListView`'s empty-hint text rather than a stale highlight
