## 1. Split shared Create/Delete wiring

- [x] 1.1 In `Components/DrillableListView.cs`, replace `EnableCreateDelete()` with two
      independent methods `EnableCreate()` and `EnableDelete()`, each with its own activation
      flag (replacing `_createDeleteEnabled`).
- [x] 1.2 Update the `Shortcuts` property to append the "New" hint only when `EnableCreate()` was
      called and the "Delete" hint only when `EnableDelete()` was called, independently.

## 2. Update KV bucket list to the split API

- [x] 2.1 In `KVStore/BucketListView.cs`, replace the `EnableCreateDelete()` call with
      `EnableCreate(); EnableDelete();` — no other behavior change.

## 3. Add Create+Delete to the OBJ bucket list

- [x] 3.1 In `ObjStore/BucketListView.cs`, remove the `public new event Action? CreateRequested`
      field, its backing Ctrl+N wiring in the constructor, and the `Shortcuts` override that
      raises it — rely on the base class's inherited `CreateRequested`/`DeleteRequested` instead.
- [x] 3.2 Call `EnableCreate(); EnableDelete();` in the constructor in place of the removed manual
      wiring.

## 4. Wire bucket deletion into the OBJ tab

- [x] 4.1 In `ObjStore/ObjTab.cs`, subscribe `_listView.DeleteRequested` to a new
      `TryDeleteBucketAsync`, mirroring `KVStore/KvTab.TryDeleteBucketAsync`: confirm via
      `MessageBox.Query` naming the bucket, call `_obj.DeleteObjectStore(name, default)`, refresh
      the list with the neighbor (`_listView.NeighborIdentity(name)`) selected, and show an
      error `MessageBox` on failure without altering the list.

## 5. Update specs and verify

- [x] 5.1 Confirm `openspec/changes/add-obj-bucket-delete/specs/nats-obj/spec.md` and
      `specs/drillable-list/spec.md` deltas match the implemented behavior.
- [x] 5.2 Build (`dotnet build src/lazynats.sln`) and manually verify via tmux: Ctrl+D on a
      highlighted OBJ bucket prompts, deletes on confirm, refreshes with a neighbor selected, and
      has no effect at the object level.
