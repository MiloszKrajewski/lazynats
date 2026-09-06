## 1. BucketListView wiring

- [x] 1.1 Add `Command.DeleteAll` + `Key.D.WithCtrl` binding to `KVStore/BucketListView.cs`,
      raising a `DeleteRequested` event (same shape as `Streams/StreamListView.cs`'s existing
      `Command.DeleteAll`/Ctrl+D block, and as this file's own existing `Command.New`/Ctrl+N)
- [x] 1.2 Append a `ShortcutHint(Key.D.WithCtrl, "Delete", () => DeleteRequested?.Invoke())` to
      `BucketListView`'s `Shortcuts` override, alongside the existing "New" hint, so it surfaces
      in the status bar

## 2. KvTab delete handling

- [x] 2.1 In `KVStore/KvTab.cs`, wire `_listView.DeleteRequested += () => _ =
      TryDeleteBucketAsync()`
- [x] 2.2 Implement `TryDeleteBucketAsync`: no-op if `_listView.SelectedBucket` is `null`;
      otherwise resolve the name via `BucketName.From(status)` and confirm via
      `MessageBox.Query(App!, DialogText.Pad("Delete Bucket"), DialogText.Pad($"Delete bucket
      '{name}'? This cannot be undone."), "_Delete", "_Cancel")` with `_Delete` first / `_Cancel`
      last (Cancel is the Enter-activated default per `doc/terminal-gui-howto.md` gotcha #1);
      proceed only when the result is index `0`
- [x] 2.3 On confirm, `await _kv.DeleteStoreAsync(name)`; on success, `_ =
      RefreshListAsync(neighborName)`, where `neighborName` is computed from `_items` before the
      delete call (below the deleted bucket, or above it if it was last) via a new
      `NeighborBucketName` helper mirroring `StreamsTab.NeighborStreamName`, comparing
      `BucketName.From(item)` against `name`
- [x] 2.4 On `DeleteStoreAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, DialogText.Pad("Delete Bucket Failed"),
      DialogText.Pad(ex.Message), "_Ok")` — body is exactly `ex.Message`, no other text; no dialog
      to reopen/reseed

## 3. Verification

- [x] 3.1 Manual pass via tmux against a real `nats-server`: with at least two buckets at the
      bucket level, highlight one, Ctrl+D, confirm the prompt names that bucket
- [x] 3.2 Manual pass: press Enter on the open prompt without navigating — confirm nothing is
      deleted (Cancel is the default) and the bucket list is unchanged
- [x] 3.3 Manual pass: choose Delete — confirm the bucket disappears from the list and (via
      `.bin`'s `nats kv ls` or the app itself) no longer exists on the server, and that highlight
      lands on the expected neighbor
- [x] 3.4 Manual pass: choose Cancel, and separately press Esc — confirm neither deletes anything
- [x] 3.5 Manual pass: descend into a bucket's key level, press Ctrl+D — confirm it has no effect
      on any bucket (key level stays read-only, per the narrowed "Read-Only Tab" requirement)
- [x] 3.6 Manual pass: attempt delete against a bucket that no longer exists server-side (e.g.
      deleted concurrently by another client) — confirm `MessageBox.ErrorQuery` shows the server's
      error and the list is left as-is until the next refresh
- [x] 3.7 Manual pass: delete the only bucket in the list — confirm the list ends up empty and
      shows `BucketListView`'s empty-hint text rather than a stale highlight
