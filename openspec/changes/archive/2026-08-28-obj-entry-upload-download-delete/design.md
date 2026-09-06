## Context

The OBJ tab (`src/lazynats/ObjStore/ObjTab.cs`) mirrors the KV tab's two-level bucket/entry shape
(`DrillableListView<T>` for both lists, `EditFrame`-wrapped, `PollingDetailsView` for the RHS
panel), but at the object level it's currently declared read-only
(`openspec/specs/nats-obj/spec.md`'s "Read-Only Tab"). `DrillableListView<T>`
(`src/lazynats/Components/DrillableListView.cs`) already exposes three independent opt-in shapes —
`EnableCreate` (Ctrl+N → `CreateRequested`), `EnableDelete` (Ctrl+D → `DeleteRequested`),
`EnableEdit` (Ctrl+E → `EditRequested`) — each hardcoding its own `Command`, key, and hint label;
`KvTab`'s key level uses all three. This change needs Create-shaped (Upload) and Delete-shaped
behavior at the object level, but no Edit (an object's content can't be modified in place — a
`PutAsync` is always a new write), plus a fourth action (Download) that no existing shape covers.
`AddCommand`/`KeyBindings` are public members `DrillableListView<T>` itself inherits from
Terminal.Gui's `View`, and its `Shortcuts` getter is `virtual` with an explicit comment inviting a
subclass to `base.Shortcuts.Append(...)` its own commands — so a subclass-only command needs no
help from the base class at all; only `ObjectListView` needs Download, and it's the only
`DrillableListView<T>` subclass with an odd-one-out fourth action.

`INatsObjStore` (`NATS.Client.ObjectStore` 2.8.2, already referenced) exposes `PutAsync(key,
Stream, leaveOpen, ct)`, `GetAsync(key, Stream, leaveOpen, ct)`, and `DeleteAsync(key, ct)` —
stream-in/stream-out, so local file transfer is a straight `FileStream` on one side and the NATS
call on the other, no buffering the whole object in memory.

## Goals / Non-Goals

**Goals:**
- Upload a local file into the drilled-into bucket under a chosen Key.
- Download the highlighted object's content to a local file path.
- Delete the highlighted object, matching the existing bucket/key delete confirm-then-delete
  shape.
- Let the user browse to a local path via Terminal.Gui's built-in file dialog, as an alternative
  to typing it.
- Keep object-level, bucket-level, and KV-level shortcut mnemonics unambiguous — no two actions at
  the same level sharing a first letter.

**Non-Goals:**
- Editing an existing object's content in place (not something the Object Store API supports —
  see `nats-obj`'s existing rationale for excluding Edit).
- Progress reporting / cancellation mid-transfer (objects are typically small config/asset blobs
  in this tool's use case; a future change can add a progress bar if large-object transfers turn
  out to matter).
- Multi-file / directory upload or download.
- Changing object metadata (description, headers) — out of scope, unrelated to this change.

## Decisions

### Decision 1: Keybindings — Ctrl+N Upload, Ctrl+D Delete, Ctrl+S Download

Upload reuses the app-wide "Ctrl+N = New/Create" convention (`ListEditorView`, `DrillableListView`
itself, both KV levels, both OBJ/KV bucket levels) rather than inventing a separate mnemonic — an
upload *is* the object-level "create a new item" action, it just collects a file path instead of a
typed value. Delete keeps the equally-established Ctrl+D. Download cannot also start with D
without colliding with Delete (the concern that motivated this change), so it takes Ctrl+S ("Save
[to disk]"), matching the built-in `Command.Save` (`Terminal.Gui.Input.Command`) the same way
Upload/Delete already reuse `Command.New`/`Command.DeleteAll`.

**Alternatives considered:**
- Ctrl+U for Upload / Ctrl+D for Delete / Ctrl+S for Download — keeps Upload's mnemonic closer to
  the literal word, but breaks the established "Ctrl+N is always the level's creating action"
  pattern for no real benefit, and the object level would then be the only list in the app where
  Ctrl+N doesn't mean "create/add here."
- Ctrl+N Upload / Ctrl+D Delete / Ctrl+O Download ("Open") — rejected because "Open" reads as
  viewing/loading something into the app, which is the opposite of what Download does (it writes
  *out*).

### Decision 2: Download is wired entirely inside `ObjectListView`, not added to `DrillableListView<T>`

`ObjectListView` adds its own `DownloadRequested` event and, in its constructor, calls the
inherited (public, from `View`) `AddCommand(Command.Save, ...)` and
`KeyBindings.Add(Key.S.WithCtrl, Command.Save)` directly — the same two calls `EnableEdit()` makes
today, just made by the subclass instead of the shared base. It overrides the base class's
`virtual Shortcuts` property to `base.Shortcuts.Append(new ShortcutHint(Key.S.WithCtrl, "Download",
...))`, which is precisely the pattern `DrillableListView<T>`'s own comment on `Shortcuts`
describes for "a subclass with its own navigation commands beyond those shapes."

This was originally drafted as a fourth `Enable*` shape on `DrillableListView<T>` itself, but that
adds shared-base-class surface for a capability exactly one of six subclasses
(`StreamListView`/`ConsumerListView`/`BucketListView` ×2/`KeyListView`/`ObjectListView`) needs, and
`DrillableListView<T>` already documents the alternative as its intended extension point — so
touching the base class at all would be solving an already-solved problem. Keeping Download local
to `ObjectListView` means `DrillableListView.cs` is untouched by this change, and the "Command
distinct from New/Edit/Delete without hijacking Edit's semantics" concern from the proposal is
resolved the same way, just one class down.

**Alternatives considered:**
- A fourth `EnableDownload()` shape on `DrillableListView<T>` (Decision 2 in an earlier draft of
  this document) — rejected per the above: no second caller exists or is anticipated, and the base
  class already documents the local-override path as correct for a one-off command.
- A fully generic `EnableAction(Command, Key, string label)` shape — same objection, doubled: it
  generalizes a mechanism with one caller, and it's a bigger surface than the one-off override it
  would replace.

### Decision 3: One dialog type, `ObjectFileDialog`, driving both Upload and Download

Rather than two dialog classes, one `Dialog<ObjectFileTransfer>` (analogous to `CreateKeyDialog`'s
`isEdit` toggle) takes an `isUpload` flag:
- **Upload**: Key is an editable `TextField` (parallel to `CreateKeyDialog`'s Name field),
  seeded empty; local Path is an editable `TextField`, validated to be a path to an existing,
  readable file.
- **Download**: Key is shown but disabled/locked to the highlighted object's name (parallel to
  `CreateKeyDialog`'s edit-mode Name field); local Path is editable, validated to be non-empty and
  a syntactically valid path (existence is not required — downloading creates the file).

Both modes share a Path-field "Browse" trigger (F2, chosen to avoid colliding with the dialog's
own Tab-between-fields and Enter-does-nothing-on-a-field conventions already established by
`CreateKeyDialog`/`CreateBucketDialog`) that runs Terminal.Gui's built-in `OpenDialog` (Upload —
`MustExist = true`) or `SaveDialog` (Download) modally via `App!.Run(dialog)`, and on a
non-cancelled result (`!dialog.Canceled`) copies `dialog.Path` into the Path `TextField` and
re-validates.

**Alternatives considered:**
- Two separate dialog classes (`UploadObjectDialog`/`DownloadObjectDialog`) — more files for very
  little divergence (same two fields, same frame/button chrome); rejected in favor of one dialog
  with a mode flag, mirroring how `CreateKeyDialog`/`CreateBucketDialog` already use `isEdit`
  rather than splitting into Create/Edit pairs.
- Free-text-only Path field, no Browse trigger — simpler, but the user explicitly asked for a
  file-browser popup during scoping; Terminal.Gui already ships `OpenDialog`/`SaveDialog`, so this
  costs one more keybinding inside the dialog rather than a new dependency.

### Decision 4: Transfer runs stream-to-stream through `PutAsync`/`GetAsync`, on the calling `Task`

`OpenCreateBucketDialog`/`TryCreateBucketAsync`'s existing pattern (run dialog synchronously on
the UI thread via `App!.Run(dialog)`, then `_ = TryXAsync(...)` fires an async continuation that
`App?.Invoke`s back for any UI mutation) is reused unchanged for Upload/Download/Delete:
- `TryUploadAsync`: open the local file as a `FileStream` (read), `await
  store.PutAsync(key, stream, leaveOpen: false, ct)`, then refresh the object list selecting the
  new key.
- `TryDownloadAsync`: open the local path as a `FileStream` (create/truncate, write), `await
  store.GetAsync(key, stream, leaveOpen: false, ct)`.
- `TryDeleteAsync`: `await store.DeleteAsync(key, ct)`, matching `TryDeleteBucketAsync`'s
  confirm-prompt-then-delete-then-refresh-with-neighbor-selected shape exactly.

A local file I/O failure (permission denied, disk full, path's directory missing) surfaces through
the same `catch (Exception ex)` → modal error → reopen-dialog-with-prior-values path already used
for server-side failures, since from the dialog's perspective both are just "the operation
failed," and `CreateKeyDialog`'s reopen-on-failure UX (design.md's existing "seed carries the
values to show" pattern in `ObjTab`) already generalizes to that.

## Risks / Trade-offs

- **[Risk] Large objects block the UI thread's async continuation for the duration of the
  transfer, with no progress indication.** → Mitigation: out of scope per Non-Goals; the status
  bar / error dialog still reports success or failure once the transfer completes, so the user
  isn't left with silent uncertainty, just no live progress.
- **[Risk] Uploading under a Key that already exists silently overwrites/creates a new revision
  (Object Store `PutAsync` doesn't error on an existing key).** → Mitigation: match Object Store's
  own semantics (a Put always succeeds and versions forward) rather than adding a client-side
  existence check the server doesn't enforce; this is consistent with `nats put` CLI behavior.
- **[Risk] `OpenDialog`/`SaveDialog` are less exercised in this codebase than the app's own
  dialogs — v2's newer, previously-unused surface.** → Mitigation: scope their use to the Browse
  trigger only; the Path `TextField` remains the primary, always-available input, so a problem
  with the file-browser popup degrades to "type the path" rather than blocking the feature.

## Open Questions

- None outstanding — keybindings and path-input mechanism were confirmed with the user during
  scoping (Decision 1, Decision 3).
