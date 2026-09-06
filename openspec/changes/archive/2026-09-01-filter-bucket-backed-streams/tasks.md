## 1. Shared BucketName Helper

- [x] 1.1 Create `src/lazynats/Components/BucketName.cs`: `partial class BucketName` with
      `[GeneratedRegex(@"^KV_(?<bucket>.+)$")]`/`[GeneratedRegex(@"^OBJ_(?<bucket>.+)$")]` and
      `TryGetKvBucketName(StreamConfig)`/`TryGetObjBucketName(StreamConfig)` returning `string?`
      per design.md's "Classification" decision (name match, then subjects-rooted-at
      `$KV.<name>.`/`$O.<name>.` check).
- [x] 1.2 Delete `src/lazynats/Values/BucketName.cs` and `src/lazynats/Objects/BucketName.cs`.

## 2. Values Tab: Wrapper Item + Shared Classification

- [x] 2.1 Add `internal sealed record KvBucketItem(string Name, NatsKVStatus Status)` (near
      `BucketListView.cs` or `ValuesTab.cs`, wherever the existing per-tab types live).
- [x] 2.2 `ValuesTab`'s bucket-list refresh: iterate `GetStatusesAsync()`, keep only items where
      `BucketName.TryGetKvBucketName(status.Info.Config) is { } name`, and build
      `new KvBucketItem(name, status)` for the list.
- [x] 2.3 `BucketListView` (Values): change `DrillableListView<NatsKVStatus>` to
      `DrillableListView<KvBucketItem>`; `GetIdentity` reads `item.Name`.
- [x] 2.4 `BucketNamePresenter` (Values): `Format(KvBucketItem value) => value.Name`.
- [x] 2.5 `BucketDetails`/`ValuesTab`: every remaining `BucketName.From(status)` call becomes
      `item.Name` from the highlighted `KvBucketItem` (`SetTarget`, create/edit/delete flows,
      `NatsKVConfig` construction, etc.) — `BucketDetails`'s own polling (`TInfo = NatsKVStatus`,
      fetched fresh via `GetStoreAsync(bucket).GetStatusAsync()`) is unaffected, since it already
      takes the bare name as its target string.

## 3. Objects Tab: Wrapper Item + Shared Classification

- [x] 3.1 Add `internal sealed record ObjBucketItem(string Name, StreamInfo Info)`.
- [x] 3.2 `ObjectsTab`'s bucket-list refresh: iterate the plain stream list, keep only items where
      `BucketName.TryGetObjBucketName(stream.Config) is { } name`, and build
      `new ObjBucketItem(name, stream)` for the list.
- [x] 3.3 `BucketListView` (Objects): change `DrillableListView<StreamInfo>` to
      `DrillableListView<ObjBucketItem>`; `GetIdentity` reads `item.Name`.
- [x] 3.4 `BucketNamePresenter` (Objects): `Format(ObjBucketItem value) => value.Name`.
- [x] 3.5 `BucketDetails`/`ObjectsTab`: every remaining `BucketName.From(stream)` call becomes
      `item.Name` from the highlighted `ObjBucketItem`.

## 4. Streams Tab Exclusion

- [x] 4.1 `StreamsTab.RefreshListAsync`: skip any stream where
      `BucketName.TryGetKvBucketName(stream.Info.Config)` or
      `BucketName.TryGetObjBucketName(stream.Info.Config)` is non-null, before adding it to the
      list.

## 5. Verification

- [x] 5.1 `dotnet build src/lazynats.sln` succeeds (source-generated regex compiles cleanly under
      `PublishAot`).
- [x] 5.2 Against a real NATS server with a mix of plain streams, a KV bucket, and an Object Store
      bucket: confirm `2:Streams` lists only the plain streams; `3:Values`/`4:Objects` list their
      respective buckets unchanged from before.
- [x] 5.3 Create a plain stream named `KV_decoy` with unrelated subjects (not
      `$KV.decoy.>`): confirm it appears in `2:Streams` and does *not* appear in `3:Values`.
- [x] 5.4 Confirm Ctrl+R refresh, Ctrl+F filter, create/edit/delete still work end-to-end on
      `3:Values`/`4:Objects` bucket lists after the `KvBucketItem`/`ObjBucketItem` switch.
