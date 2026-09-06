> **Note:** All tasks below reference `src/lazynats/Obj/` per the original design, but on this
> Windows dev machine the filesystem is case-insensitive, so `Obj/` collides with the existing
> build-output directory `src/lazynats/obj/` (MSBuild's default globs exclude `obj\**` from
> compilation). Implementation uses `src/lazynats/ObjStore/` (namespace `lazynats.ObjStore`)
> instead, confirmed with the user during apply. All other file/type names are unchanged.

## 1. DI wiring

- [x] 1.1 In `Program.cs`, add `var obj = jetStream.CreateObjectStoreContext();` next to the
      existing `kv` line, and `services.AddSingleton(obj);` next to `services.AddSingleton(kv);`.

## 2. Bucket naming convention

- [x] 2.1 Create `src/lazynats/ObjStore/BucketName.cs` (renamed from the planned `Obj/` - see note
      below), parallel to `Kv/BucketName.cs`: `IsObjStream` checks the `OBJ_` prefix on a
      `StreamInfo.Config.Name`, `From` extracts the bare bucket name from a `StreamInfo`.

## 3. Bucket list level

- [x] 3.1 Create `src/lazynats/ObjStore/BucketListView.cs`, parallel to `Kv/BucketListView.cs`, as a
      `DrillableListView<StreamInfo>` (not `NatsKVStatus` — see design.md's "Bucket list"
      decision) with a `DescendRequested` event.
- [x] 3.2 Create `src/lazynats/ObjStore/BucketNamePresenter.cs`, parallel to
      `Kv/BucketNamePresenter.cs`, formatting a `StreamInfo` via `BucketName.From`.
- [x] 3.3 Create `src/lazynats/ObjStore/BucketDetails.cs`, parallel to `Kv/BucketDetails.cs`, as a
      `PollingDetailsView<string, NatsObjStatus>` fetching via
      `(await _obj.GetObjectStoreAsync(bucket)).GetStatusAsync()`. Rows: Bucket, Compressed, blank
      separator, Objects (`Info.State.Messages`), Bytes, Replicas (`Info.Config.NumReplicas`), Max
      Age (`Info.Config.MaxAge`).

## 4. Object list level

- [x] 4.1 Create `src/lazynats/ObjStore/ObjectListView.cs`, parallel to `Kv/KeyListView.cs`, as a
      `DrillableListView<string>` with an `AscendRequested` event bound to Esc/Backspace.
- [x] 4.2 Create `src/lazynats/ObjStore/ObjectNamePresenter.cs`, parallel to `Kv/KeyNamePresenter.cs`.
- [x] 4.3 Create `src/lazynats/ObjStore/ObjectDetails.cs`, parallel to `Kv/KeyDetails.cs`, as a
      `PollingDetailsView<(string Bucket, string Name), ObjectMetadata>`. `FetchAsync` calls
      `store.GetInfoAsync(name, showDeleted: false)` inside a
      `try`/`catch (NatsObjNotFoundException)` returning `null` (per design.md's "Object detail
      panel" decision — do not catch the broader `NatsObjException`). Rows: Name, Description,
      Size, Chunks, Digest, Modified (`MTime`). No `BuildBody` override — metadata only, never
      content (per the proposal and the "Object Detail Panel Shows Metadata Only" requirement).

## 5. Tab assembly

- [x] 5.1 Create `src/lazynats/ObjStore/ObjTab.cs`, parallel to `Kv/KvTab.cs`: two-level view toggling
      bucket-list/object-list frames and detail panes via `Visible`, `_currentBucket` tracking
      drill-down state, `RefreshListAsync` iterating `_jetStream.ListStreamsAsync()` filtered by
      `BucketName.IsObjStream`, `RefreshObjectListAsync` iterating
      `(await _obj.GetObjectStoreAsync(bucket)).ListAsync(new NatsObjListOpts())` and collecting
      `.Name` from each yielded `ObjectMetadata` (default `ShowDeleted: false` — deleted objects
      excluded per the object-list requirement), `Descend`/`Ascend` mirroring `KvTab`'s exactly,
      `StatusChanged` event for surfacing errors.

## 6. MainWindow wiring

- [x] 6.1 In `MainWindow.cs`: resolve `INatsObjContext` from `Services.Root`, construct `objTab`
      with title `" 5:OBJ "` and the same `Padding` as `kvTab`, add it to `tabs.Add(...)` after
      `kvTab`.
- [x] 6.2 Add an `objTabShortcut` (`Key.D5.WithAlt`, `BindKeyToApplication = true`) wired to
      `tabs.Value = objTab`, and an `objStatusShortcut` relaying `objTab.StatusChanged` — both
      appended to the `_statusBar` construction array, mirroring `kvTabShortcut`/
      `kvStatusShortcut` exactly.

## 7. Verification

- [x] 7.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 7.2 Using `.bin/nats.exe object add <bucket>` / `object put` against the local dev server,
      launch the app (via tmux per `CLAUDE.md`) and confirm: OBJ bucket list populates, Alt+5
      switches to it, Enter descends into an object list, highlighting an object shows metadata
      with no content/body rendered, Esc/Backspace ascends, Ctrl+R refreshes both levels, deleting
      an object server-side and letting the poll tick clears its detail panel without re-fetching
      the list.

      **Found + fixed during this verification pass (out of the original task list, approved by
      the user):** the "poll tick clears the detail panel" scenario initially failed - not due to
      OBJ code, but a pre-existing bug in the shared `Components/PollingDetailsView.cs`, also
      reproduced live against the shipped KV tab (deleted a KV key server-side, stale details
      never cleared past the 3s poll interval). Its poll pipeline filtered out *every* null
      `FetchAsync` result with `.Where(info => info is not null)`, which was meant to drop only
      error results but also silently dropped a legitimate "not found" null, so `Show(null)` never
      fired from a poll tick. Fixed by having `FetchInternalAsync` return `(bool Success, TInfo?
      Info)` - `Success: false` only on a caught exception (still keeps old content, unchanged
      behavior) - and filtering on `Success` instead of `info is not null`, so a genuine null
      result now reaches `Show(null)`. Re-verified live against both OBJ and KV after the fix;
      both now clear correctly on the next poll tick. This satisfies nats-obj's "A Deleted Object
      Renders As No Selection" requirement and (as a side effect) nats-kv's equivalent
      "A Deleted Key Renders As No Selection" requirement, previously unmet.
- [x] 7.3 Clean up any probe buckets/objects created for manual verification
      (`.bin/nats.exe object rm <bucket> --force`). Also removed a probe KV bucket/key created
      solely to reproduce and verify the `PollingDetailsView` fix above.
