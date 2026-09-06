## Why

`doc/UI.md` reserves a 5th management tab ("5:OBJ") for browsing JetStream Object Stores, and the
KV tab already establishes the two-level (bucket list / entry list) read-only browsing pattern for
JetStream-backed stores. OBJ stores are the remaining store type with no UI at all today.

## What Changes

- Add an `OBJ` management tab (`5:OBJ`, Alt+5), mirroring the KV tab's structure: bucket list on
  the left, drill down via Enter into the highlighted bucket's object list, Esc/Backspace climbs
  back up, Ctrl+R manually refreshes whichever list is shown.
- Bucket list is sourced from JetStream's stream list (filtered to the `OBJ_<bucket>` naming
  convention), since `INatsObjContext` — unlike `INatsKVContext` — has no bulk bucket-status
  listing call.
- Bucket detail panel: compression setting, size/backing-store stats, replica count — the same
  shape as the KV bucket panel, sourced per-highlighted-bucket via `NatsObjStatus`.
- Object list is sourced from the bucket's `ListAsync()` (object names only, not full metadata,
  matching the KV key list's shape).
- Object detail panel shows metadata only — name, description, size, chunk count, digest,
  modified time, deleted flag — **never the object's content/bytes**. Unlike the KV key panel
  (which renders the value as UTF-8 text), an object's payload is arbitrary binary data with no
  safe universal text rendering, and downloading/decoding it is out of scope for this change.
- Tab is read-only: no create/delete/upload affordance for buckets or objects, same restriction as
  the KV tab.

## Capabilities

### New Capabilities
- `nats-obj`: OBJ store management tab — bucket list/detail, object list/detail (metadata only,
  no content), navigation between levels, manual-refresh-only lists, periodic detail polling.

### Modified Capabilities
(none — `polling-details`, `drillable-list`, `edit-frame`, and `tab-content-structure` are reused
as-is, the same way the KV tab reused them without requiring spec changes)

## Impact

- New `src/lazynats/Obj/` folder (`ObjTab.cs`, `BucketListView.cs`, `BucketDetails.cs`,
  `ObjectListView.cs`, `ObjectDetails.cs`, `BucketName.cs`, presenters), mirroring
  `src/lazynats/Kv/`.
- `Program.cs`: register `INatsObjContext` (`jetStream.CreateObjectStoreContext()`) as a DI
  singleton, alongside the existing `INatsKVContext` registration.
- `MainWindow.cs`: construct `ObjTab`, add it to `ManagementTabs`, wire its Alt+5 shortcut and
  status-bar message relay, following the existing KV tab's wiring exactly.
- No changes to `NATS.Client.ObjectStore` package reference — already present in
  `lazynats.csproj` (2.8.2, unused until now).
