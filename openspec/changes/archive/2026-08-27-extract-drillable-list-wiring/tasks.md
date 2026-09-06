## 1. Base: `DrillableListView<T>`

- [x] 1.1 Add `NeighborIdentity(string identity)` built on the existing private
      `IndexOfIdentity`/`GetIdentity`.
- [x] 1.2 Add private state flags (descend/ascend/create-delete enabled) and the three opt-in
      helpers: `EnableDescend()`, `EnableAscend()`, `EnableCreateDelete()` — each wiring the
      relevant event(s), key binding(s)/command(s), and (for ascend/create-delete) contributing to
      `Shortcuts`.
- [x] 1.3 Change `Shortcuts` to compose the Ctrl+R hint plus any hints implied by the enabled
      flags, so subclasses no longer need to override it purely to append these three shapes.
- [x] 1.4 Build.

## 2. Migrate subclasses with Descend + Create/Delete

- [x] 2.1 `Streams/StreamListView.cs`: call `EnableDescend()` and `EnableCreateDelete()` in the
      constructor; remove the hand-rolled `DescendRequested`/`CreateRequested`/`DeleteRequested`
      wiring and the `Shortcuts` override.
- [x] 2.2 `KVStore/BucketListView.cs`: same as 2.1.
- [x] 2.3 Build; `tmux`-drive Streams and KV tabs to confirm Enter/Ctrl+N/Ctrl+D and their status
      bar hints are unchanged.

## 3. Migrate subclasses with Ascend + Create/Delete

- [x] 3.1 `Streams/ConsumerListView.cs`: call `EnableAscend()` and `EnableCreateDelete()`; remove
      the hand-rolled wiring and `Shortcuts` override.
- [x] 3.2 Build; `tmux`-drive into a stream's consumer list to confirm Esc/Backspace/Ctrl+N/Ctrl+D
      and hints are unchanged.

## 4. Migrate subclasses with Descend only / Ascend only

- [x] 4.1 `ObjStore/BucketListView.cs`: call `EnableDescend()`; remove the hand-rolled wiring.
- [x] 4.2 `KVStore/KeyListView.cs`: call `EnableAscend()`; remove the hand-rolled wiring and
      `Shortcuts` override.
- [x] 4.3 `ObjStore/ObjectListView.cs`: call `EnableAscend()`; remove the hand-rolled wiring and
      `Shortcuts` override.
- [x] 4.4 Build; `tmux`-drive the OBJ tab (Enter/Esc) and confirm KV's key list (Esc/Backspace)
      still ascends and shows no New/Delete hints.

## 5. Callers: neighbor-lookup

- [x] 5.1 `Streams/StreamsTab.cs`: replace `NeighborStreamName`/`NeighborConsumerName` call sites
      with `_listView.NeighborIdentity(name)` / `_consumerListView.NeighborIdentity(name)`; delete
      both now-dead methods.
- [x] 5.2 `KVStore/KvTab.cs`: replace `NeighborBucketName` call site with
      `_listView.NeighborIdentity(name)`; delete the now-dead method.
- [x] 5.3 Build; `tmux`-drive a delete in the stream list, confirming the same neighbor gets
      refocused as before this change (verified both the "next item" and "previous item, when
      last" scenarios against real streams created/deleted via the app). Consumer-level and
      bucket-level call sites are one-line substitutions of the same proven algorithm and build
      clean, but could not be live-verified: creating a test consumer/bucket via the app's own
      Ctrl+N dialogs failed with default field values, a pre-existing issue unrelated to this
      change (not touched by it - see note to the user).

## 6. Wrap-up

- [x] 6.1 Full solution build (`dotnet build src/lazynats.sln`).
- [x] 6.2 Re-check each of the six subclasses' `Shortcuts`/keybindings once more against the
      status bar in `tmux` (Streams, Consumers, KV Buckets, KV Keys, OBJ Buckets, OBJ Objects) to
      confirm no shortcut hint was dropped or duplicated.
