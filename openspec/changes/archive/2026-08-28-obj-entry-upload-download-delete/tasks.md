## 1. `ObjectListView` Download command

- [x] 1.1 In `ObjectListView` (`src/lazynats/ObjStore/ObjectListView.cs`), add a
      `DownloadRequested` event, and in its constructor call `AddCommand(Command.Save, ...)` and
      `KeyBindings.Add(Key.S.WithCtrl, Command.Save)` directly (both public on `View`, no
      `DrillableListView<T>` change needed) to raise it.
- [x] 1.2 Override `Shortcuts` on `ObjectListView` to `base.Shortcuts.Append(new
      ShortcutHint(Key.S.WithCtrl, "Download", () => DownloadRequested?.Invoke()))`, matching the
      pattern `DrillableListView<T>`'s own `Shortcuts` doc comment describes for a subclass-only
      command.

## 2. Object transfer dialog

- [x] 2.1 Add `ObjectFileTransfer` record (Key, Path) in `src/lazynats/ObjStore/` mirroring
      `NewKeyOptions`' shape.
- [x] 2.2 Add `ObjectFileDialog : Dialog<ObjectFileTransfer>` with an `isUpload` constructor flag,
      mirroring `CreateKeyDialog`'s field layout (Key `TextField`, Path `TextField`, `EditFrame`-
      wrapped, Cancel + Upload/Download buttons) — Key disabled/seeded in download mode, exactly
      as `CreateKeyDialog`'s Name is disabled/seeded in edit mode.
- [x] 2.3 Wire field validation: Key non-empty (upload only), Path non-empty and (upload only)
      pointing to an existing readable file — same red-text `SetFieldValidity` pattern as
      `CreateKeyDialog`.
- [x] 2.4 Add the in-dialog Browse trigger (F2) on the Path field: opens Terminal.Gui's
      `OpenDialog` (upload, `MustExist = true`) or `SaveDialog` (download) via `App!.Run(...)`,
      and on `!Canceled` copies `Path` into the Path field and re-validates.

## 3. `ObjTab` wiring

- [x] 3.1 In `ObjectListView`'s constructor, call the shared `EnableCreate()` and `EnableDelete()`
      (no `EnableEdit()`) alongside the Download wiring from Task 1, following `KeyListView`'s
      pattern of enabling shapes from its constructor.
- [x] 3.2 In `ObjTab.cs`, subscribe `_objectListView.CreateRequested` → open
      `ObjectFileDialog(isUpload: true)` → `TryUploadObjectAsync`.
- [x] 3.3 Subscribe `_objectListView.DownloadRequested` → guard on `SelectedObject`/`_currentBucket`
      → open `ObjectFileDialog(isUpload: false, seed: selected object's name)` →
      `TryDownloadObjectAsync`.
- [x] 3.4 Subscribe `_objectListView.DeleteRequested` → `TryDeleteObjectAsync`, mirroring
      `TryDeleteBucketAsync`'s confirm-prompt → delete → `RefreshObjectListAsync(neighborName)`
      shape (`_objectListView.NeighborIdentity(name)`).
- [x] 3.5 Implement `TryUploadObjectAsync`: open the local file `FileStream` (read), `await
      store.PutAsync(key, stream, leaveOpen: false, ct)`, `RefreshObjectListAsync(selectName:
      key)`; on failure, modal error + reopen dialog with prior values (mirror
      `TryCreateBucketAsync`).
- [x] 3.6 Implement `TryDownloadObjectAsync`: open the local path `FileStream` (create/truncate,
      write), `await store.GetAsync(key, stream, leaveOpen: false, ct)`; on failure, modal error +
      reopen dialog with prior Path.
- [x] 3.7 Implement `TryDeleteObjectAsync`: confirm via `MessageBox.Query` (naming the object,
      Cancel as default), `await store.DeleteAsync(key, ct)`, refresh with neighbor selected; on
      failure, modal error only (list left unchanged), mirroring `TryDeleteBucketAsync`.

## 4. Spec cleanup

- [x] 4.1 Remove the "no mutation affordance" wiring implied by the old read-only object level
      (verify no other code path assumed `EnableCreate`/`EnableDelete` would never be called, or
      that `ObjectListView`'s `Shortcuts` would never grow a fourth hint, on `ObjectListView`).

## 5. Verification

- [x] 5.1 `dotnet build src/lazynats.sln` compiles clean.
- [x] 5.2 Via tmux against a real `nats-server` (per `CLAUDE.md`'s tmux workflow): upload a small
      file, confirm it appears highlighted in the object list and its detail panel shows correct
      size/digest.
- [x] 5.3 Download that same object to a new local path via tmux + the `nats` CLI (`.bin/`) or a
      file read, and confirm the bytes round-trip.
- [x] 5.4 Delete the object via Ctrl+D, confirm the confirmation prompt names it and the list
      updates after confirming.
- [x] 5.5 Confirm Ctrl+N/Ctrl+S/Ctrl+D have no effect at the bucket level, and Ctrl+E remains
      absent from the object level's Shortcuts hint bar.
