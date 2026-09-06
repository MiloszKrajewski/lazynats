## Why

The OBJ tab's object level is currently read-only ("Read-Only Tab" in `nats-obj`): there is no way
to get a file into or out of an Object Store bucket, or to remove one object without deleting the
whole bucket, from inside lazynats — every such operation currently requires the `nats` CLI or
another client. Object Store entries are files, not structured key/value text, so the existing
KV-style New/Edit/Delete entry editor (`openspec/specs/nats-kv/spec.md`'s Create/Edit/Delete Key)
doesn't fit as-is: there's no in-app value to type, only a local file to move in or out, and
editing an existing object's content in place isn't a thing NATS objects support (a "put" is
always a new object/revision).

## What Changes

- **BREAKING**: Removes the OBJ tab's "Read-Only Tab" requirement. The object level gains three
  mutating operations: Upload, Download, Delete. Edit is intentionally not provided — an object's
  content can only be replaced by uploading over it (a new Put), not modified in place.
- Upload (Ctrl+N): opens a modal dialog collecting the object Key and a local file path (a text
  field, plus a keyboard-triggered file-browser popup — Terminal.Gui's built-in file dialog — to
  pick the path instead of typing it). Confirming streams that local file's contents into the
  drilled-into bucket under the given Key and refreshes the object list.
- Download: opens the same shaped dialog (Key pre-filled and locked to the highlighted object,
  local file path via text field or the file-browser popup) and streams that object's content to
  the chosen local path. Bound to Ctrl+S ("Save to disk") rather than a D-prefixed key, since
  Ctrl+D already means Delete throughout the app (`ListEditorView`/`DrillableListView`'s shared
  New/Edit/Delete convention) and a same-letter mnemonic for two destructive/near-destructive
  object-level actions would be ambiguous.
- Delete (Ctrl+D): same confirm-then-delete shape already used for bucket deletion and KV key
  deletion — prompts for confirmation, then removes the object from the bucket and refreshes the
  list.
- `ObjectListView` wires its own Ctrl+S/`DownloadRequested` command directly (`AddCommand`/
  `KeyBindings.Add`, both public on Terminal.Gui's `View`) and overrides `Shortcuts` to append a
  "Download" hint onto `base.Shortcuts` — the extension path `DrillableListView<T>`'s own comment
  already documents for a subclass-specific command, so the shared base class is untouched. Upload
  reuses the existing shared `EnableCreate()` shape unchanged.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-obj`: replaces the "Read-Only Tab" requirement with new "Upload Object", "Download
  Object", and "Delete Object" requirements at the object level, including their dialogs and field
  validation.

## Impact

- `src/lazynats/ObjStore/ObjectListView.cs`, `ObjTab.cs`: wire Upload/Download/Delete, replacing
  the tab's current read-only object level. `DrillableListView.cs` itself is unchanged.
- New `src/lazynats/ObjStore/ObjectFileDialog.cs` (or similarly named) modal dialog for
  Upload/Download, reusing `EditFrame`/`Theme`/`DialogText` conventions from `CreateKeyDialog`/
  `CreateBucketDialog`.
- `NATS.Client.ObjectStore` (`INatsObjStore.PutAsync`/`GetAsync`/`DeleteAsync`) for the actual
  transfer/delete calls; local file I/O via `System.IO.File`/`FileStream`.
- `openspec/specs/nats-obj/spec.md`.
