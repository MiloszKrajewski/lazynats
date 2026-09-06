## 1. Shared list wiring

- [x] 1.1 Add `EnableEdit()` to `Components/DrillableListView.cs`, mirroring `EnableCreate`/
      `EnableDelete`: an `EditRequested` event, `AddCommand(Command.Edit, ...)` bound to Ctrl+E,
      and an "Edit" entry appended to `Shortcuts`.
- [x] 1.2 Confirm `EnableEdit()` is independently activatable — a list that doesn't call it gets no
      Ctrl+E behavior or hint, same as any other opt-in shape.

## 2. Dialog edit mode — Stream

- [x] 2.1 Add an `isEdit = false` constructor parameter to `Streams/CreateStreamDialog.cs`. When
      `true`: `Title` reads "Edit Stream", the confirm button's text/mnemonic changes from
      `"_Create"` to `"_Save"`, and `_nameField`/`_retentionDropDown` are disabled
      (`Enabled = false`) rather than removed.
- [x] 2.2 Confirm `UpdateValidity()` needs no edit-mode branch — a disabled field's seeded value
      came from the server, so it's already valid.

## 3. Dialog edit mode — Consumer

- [x] 3.1 Add the same `isEdit` parameter to `Streams/CreateConsumerDialog.cs`: title → "Edit
      Consumer", button → "_Save", `_nameField`/`_ackPolicyDropDown`/`_deliverPolicyDropDown`
      disabled.

## 4. Dialog edit mode — KV Bucket

- [x] 4.1 Add the same `isEdit` parameter to `KVStore/CreateBucketDialog.cs`: title → "Edit
      Bucket", button → "_Save", `_nameField`/`_storageDropDown` disabled.

## 5. Dialog edit mode — OBJ Bucket

- [x] 5.1 Add the same `isEdit` parameter to `ObjStore/CreateBucketDialog.cs`: title → "Edit
      Bucket", button → "_Save", `_nameField` disabled.

## 6. Streams tab — Edit Stream

- [x] 6.1 Activate `EnableEdit()` on `StreamListView` alongside its existing
      `EnableDescend`/`EnableCreate`/`EnableDelete`.
- [x] 6.2 In `StreamsTab`, wire `_listView.EditRequested` to `OpenEditStreamDialog`, following the
      `OpenCreateStreamDialog` shape. `_listView.SelectedStream` already exposes the full
      `StreamInfo` (hence `.Config`) needed to seed the dialog and to merge onto later — no new
      list-view API required.
- [x] 6.3 Implement `TryEditStreamAsync(StreamConfig original, NewStreamOptions edited)`: build
      `original with { Subjects = edited.Subjects.ToList(), MaxAge = edited.MaxAge ??
      TimeSpan.Zero }` — **not** `edited.ToStreamConfig()`, per design.md Decision 2 — and pass it
      to `_jetStream.UpdateStreamAsync`. On success, refresh the stream list keeping the edited
      stream highlighted. On failure: `MessageBox.ErrorQuery` with `ex.Message`, then reopen the
      edit dialog reseeded with the attempted values (mirrors `TryCreateStreamAsync`'s catch
      block).

## 7. Streams tab — Edit Consumer

- [x] 7.1 Activate `EnableEdit()` on `ConsumerListView` alongside its existing
      `EnableAscend`/`EnableCreate`/`EnableDelete`.
- [x] 7.2 Wire `_consumerListView.EditRequested` to `OpenEditConsumerDialog`, scoped to
      `_currentStream` the same way `OpenCreateConsumerDialog` already is.
      `_consumerListView.SelectedConsumer` already exposes the full `ConsumerInfo` (hence
      `.Config`) needed to seed and merge.
- [x] 7.3 Implement `TryEditConsumerAsync(string stream, ConsumerConfig original,
      NewConsumerOptions edited)`: build `original with { FilterSubjects = edited.FilterSubjects
      .Count > 0 ? edited.FilterSubjects.ToList() : null, FilterSubject = null }` — the explicit
      `FilterSubject = null` clears a singular filter set outside lazynats (design.md's
      "FilterSubject/FilterSubjects duality" decision) — and call `_jetStream.UpdateConsumerAsync`.
      Same refresh-on-success / ErrorQuery-and-reopen-on-failure shape as 6.3.

## 8. KV tab — Edit Bucket

- [x] 8.1 Activate `EnableEdit()` on the KV `BucketListView` alongside its existing
      `EnableDescend`/`EnableCreate`/`EnableDelete`.
- [x] 8.2 Wire `_listView.EditRequested` to `OpenEditBucketDialog` in `KvTab`.
      `_listView.SelectedBucket` exposes the full `NatsKVStatus`; since that type carries no
      `NatsKVConfig` of its own in NATS.Client.KeyValueStore 2.8.2 (only `Info.Config`, a
      `StreamConfig`, plus a top-level `LimitMarkerTTL`), a `ToNatsKVConfig` helper reconstructs
      the `NatsKVConfig` field-by-field from those two sources to seed and merge onto.
- [x] 8.3 Implement `TryEditBucketAsync(NatsKVStatus status, NatsKVConfig original,
      NewBucketOptions edited)`: build `original with { History = edited.History ?? 1, MaxAge =
      edited.MaxAge ?? TimeSpan.Zero, LimitMarkerTTL = edited.LimitMarkerTTL ?? TimeSpan.Zero }`
      and call `_kv.UpdateStoreAsync` — the real KV-level update API, not a raw stream update
      (design.md's KV-vs-OBJ asymmetry). Same refresh/error-dialog/reopen shape as 6.3.

## 9. OBJ tab — Edit Bucket

- [x] 9.1 Activate `EnableEdit()` on the OBJ `BucketListView` alongside its existing
      `EnableDescend`/`EnableCreate`/`EnableDelete`.
- [x] 9.2 Wire `_listView.EditRequested` to `OpenEditBucketDialog` in `ObjTab`.
      `_listView.SelectedBucket` already exposes the full `StreamInfo` (hence `.Config`) needed to
      seed and merge — the same object `ObjTab.RefreshListAsync` already builds via
      `_jetStream.ListStreamsAsync()`.
- [x] 9.3 Implement `TryEditBucketAsync(StreamConfig original, NewBucketOptions edited)`: build
      `original with { MaxAge = edited.MaxAge ?? TimeSpan.Zero }` and call
      `_jetStream.UpdateStreamAsync` directly (no `INatsObjContext` bucket-update call exists —
      design.md Decision 3). Same refresh/error-dialog/reopen shape as 6.3.
- [x] 9.4 Before treating this as done, empirically verify the OBJ MaxAge update against a live
      `nats-server` (throwaway bucket, same approach used for Stream/Consumer/KV during design),
      per design.md's Open Question — it was inferred from the identical Stream/KV mechanism but
      not independently re-confirmed. Confirmed via `nats.exe object add test-edit-obj-bucket` +
      `nats.exe stream edit OBJ_test-edit-obj-bucket --max-age=1h -f` against live nats-server
      2.12.8: MaxAge updated cleanly (0s -> 1h0m0s), same mechanism as Stream/KV. Throwaway bucket
      cleaned up afterward.

## 10. Build and verify

- [x] 10.1 `dotnet build src/lazynats.sln` — confirm the solution compiles.
- [x] 10.2 Manual verification via `tmux` + `dotnet run` (per `CLAUDE.md`) against a real
      `nats-server`: Ctrl+E on each of the four lists; confirm the locked-field set from
      design.md's table is actually disabled in each dialog; confirm a successful edit is
      reflected in the corresponding detail panel; confirm at least one forced server-side
      rejection (e.g. an unexpected error from a locked field somehow reaching the server) surfaces
      through the error dialog and reopens the edit dialog pre-filled. Verified all four flows
      live: Stream (Subjects+MaxAge edited, Name/Retention confirmed locked/uneditable via direct
      keystroke tests), Consumer (Filter Subjects edited, Name/AckPolicy/DeliverPolicy locked),
      KV bucket (History/MaxAge/LimitMarkerTTL edited including enabling a previously-unset TTL,
      Name/Storage locked), OBJ bucket (MaxAge edited, Name locked) - each confirmed via the
      corresponding detail panel AND cross-checked against `nats.exe stream/kv/object info`.
      Forced a genuine server-side rejection (Stream Subjects edited to overlap an existing
      stream's subject) - error dialog showed "Edit Stream Failed" / "subjects overlap with an
      existing stream", and dismissing it reopened the edit dialog pre-filled with the rejected
      Subjects value, matching design.md's error-handling requirement exactly.
- [x] 10.3 `openspec validate add-resource-edit --strict` once implementation matches the specs,
      before archiving. Passed: "Change 'add-resource-edit' is valid".
