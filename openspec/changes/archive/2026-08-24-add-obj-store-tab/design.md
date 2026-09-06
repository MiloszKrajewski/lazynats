## Context

The KV tab (`src/lazynats/Kv/`, `openspec/specs/nats-kv/spec.md`) already established the
two-level LHS-list/RHS-details pattern for a JetStream-backed store, built entirely on
`DrillableListView<T>` and `PollingDetailsView<TTarget,TInfo>` (`src/lazynats/Components/`). This
change reuses both unchanged and follows the KV tab's file layout and wiring almost mechanically —
this design only calls out where OBJ's API shape forces something different.

`NATS.Client.ObjectStore` (v2.8.2) is already referenced in `lazynats.csproj` but unused.
Confirmed surface, via the installed package's XML docs and reflection against the app's own
built `bin/` output (not yet in `context7`'s indexed docs beyond quick-start snippets):

- `jetStream.CreateObjectStoreContext()` — `NATS.Net.NatsClientExtensions`, same family as
  `CreateKeyValueStoreContext()` — → `INatsObjContext`.
- **`INatsObjContext` has no bulk "list all bucket statuses" call** (`INatsKVContext`'s
  `GetStatusesAsync()` has no OBJ equivalent). Its only members are
  `CreateObjectStoreAsync`/`GetObjectStoreAsync`/`DeleteObjectStore`, all single-bucket-by-name.
- `INatsObjContext.JetStreamContext` (and `INatsObjStore.JetStreamContext`) exposes the underlying
  `INatsJSContext` — the same one `StreamsTab`/`INatsKVContext` already use.
- `INatsObjContext.GetObjectStoreAsync(bucket)` → `INatsObjStore`.
- `INatsObjStore.GetStatusAsync()` → `NatsObjStatus { string Bucket, bool IsCompressed, StreamInfo
  Info }` — same shape as `NatsKVStatus`, just no `LimitMarkerTTL`.
- `INatsObjStore.ListAsync(NatsObjListOpts, ct)` → `IAsyncEnumerable<ObjectMetadata>` (opts has
  one field, `ShowDeleted`, default `false`).
- `INatsObjStore.GetInfoAsync(key, showDeleted, ct)` → `ObjectMetadata`, **throws**
  `NatsObjNotFoundException` (a `NatsObjException` subtype) if the key doesn't exist — unlike KV's
  `TryGetEntryAsync`, there is no non-throwing lookup.
- `ObjectMetadata { string Name, Description, Bucket, Nuid, Digest; ulong Size; uint Chunks;
  DateTimeOffset MTime; bool Deleted; Dictionary<string,string> Metadata; Dictionary<string,
  string[]> Headers; MetaDataOptions Options }`. No `GetBytesAsync`/`GetAsync` call is used
  anywhere in this change — content is out of scope per the proposal.

## Goals / Non-Goals

**Goals:**
- Ship the `5:OBJ` tab per `doc/UI.md`: bucket list with stats, drill into a bucket's objects,
  object metadata detail — metadata only, never content.
- Reuse `DrillableListView`/`PollingDetailsView` exactly as they stand today (both already support
  everything needed: list mechanics, the debounced-target-change + interval poll pipeline, and the
  optional-body `BuildBody` hook this tab doesn't need to touch).

**Non-Goals:**
- No object content/bytes rendering, downloading, or decoding of any kind — `BuildBody` is left at
  its default `null` for `ObjectDetails`, matching the "metadata only" requirement in the
  proposal. This also sidesteps any binary-vs-text rendering question the KV value panel doesn't
  have to answer (a KV value is assumed textual; an OBJ object generally isn't).
- No mutation (create/delete/upload/link) for buckets or objects — read-only, matching KV/Streams.
- No distinct "deleted object" indicator — mirrors KV's "A Deleted Key Renders As No Selection":
  an object that's gone by poll time renders identically to nothing being highlighted.
- No use of `ListAsync`'s `ShowDeleted` option — deleted objects are excluded from the list
  entirely, same as KV never lists tombstoned keys separately.

## Decisions

### Bucket list: enumerate JetStream streams filtered by `OBJ_` prefix, not an OBJ-context bulk call
Since `INatsObjContext` has no `GetStatusesAsync()` equivalent, the bucket list can't be sourced
the same way `KvTab.RefreshListAsync` sources its list (iterate `_kv.GetStatusesAsync()`, filter
client-side). Instead, mirror `StreamsTab.RefreshListAsync`'s existing call —
`_jetStream.ListStreamsAsync()`, already used for the Streams tab — and filter to streams named
`OBJ_<bucket>`, the same convention `BucketName.IsKvStream` applies for `KV_<bucket>`. A new
`Obj/BucketName.cs` (parallel to `Kv/BucketName.cs`) owns `IsObjStream`/`From`.

This means the OBJ bucket list is built from `StreamInfo` alone (no `NatsObjStatus`), one JetStream
API round trip for the whole list, same as Streams/KV.

**Alternative considered**: call `_obj.GetObjectStoreAsync(name).GetStatusAsync()` once per
candidate stream to get a real `NatsObjStatus` for the list. Rejected — turns one round trip into
N, purely to get `IsCompressed` a screen earlier than the detail panel already shows it; the list
only needs the bucket name, which `StreamInfo.Config.Name` (minus prefix) already gives.

### Bucket detail panel: fetch `NatsObjStatus` per highlighted bucket, same shape as KV
`BucketDetails : PollingDetailsView<string, NatsObjStatus>` fetches via
`(await _obj.GetObjectStoreAsync(bucket)).GetStatusAsync()` on each poll tick — mirrors
`Kv/BucketDetails.cs` exactly, just swapping the KV types for OBJ ones. Rows: Bucket, Compressed,
then a blank separator, then Objects (`Info.State.Messages` — same "counts stream messages, not
necessarily distinct live objects" caveat KV's "Entries" label carries, labeled accordingly),
Bytes, Replicas (`Info.Config.NumReplicas`), Max Age (`Info.Config.MaxAge`). No TTL-limit row —
`NatsObjStatus` has no `LimitMarkerTTL` equivalent (that's KV-specific).

### Object list: `ListAsync()` names only, not full `ObjectMetadata`, matching KV's key-list shape
`ObjectListView` (parallel to `KeyListView`) holds `ObservableCollection<string>` of object names,
populated by iterating `store.ListAsync(new NatsObjListOpts(), ct)` and taking `.Name` from each
`ObjectMetadata` — even though the full metadata is already in hand at list time, discarding it and
re-fetching per-highlight via `GetInfoAsync` keeps the list/detail split consistent with every
other level in the app (bucket list holds names only too, not full status) and keeps
`DrillableListView<string>` reusable unchanged. The extra per-highlight round trip is the same
trade-off KV already makes for keys.

**Alternative considered**: keep the full `ObjectMetadata` list from `ListAsync` in memory and have
`OnObjectHighlightChanged` just index into it instead of polling `GetInfoAsync` at all — cheaper,
and avoids the not-found exception entirely. Rejected for this change to keep the shape identical
to KV throughout (list = names, detail = independently polled) rather than introducing a second
pattern; can revisit if OBJ buckets with many objects make the extra round trips noticeable.

### Object detail panel: catch `NatsObjNotFoundException` internally, `null` for anything else that's "not found"
`ObjectDetails.FetchAsync(bucket, name)` calls `store.GetInfoAsync(name, showDeleted: false)`
inside a `try`/`catch (NatsObjNotFoundException)` returning `null` — reproducing KV's
`TryGetEntryAsync`-based "vanished key renders as no selection" behavior despite the OBJ API being
throw-based instead of result-based. Any other exception (connection failure, server error, etc.)
is left to propagate — `PollingDetailsView.FetchInternalAsync`'s own outer catch already turns that
into an `Error` event exactly as it does for every other subclass.

Rows: Name, Description, Size (bytes), Chunks, Digest, Modified (`MTime`). No body — content is
explicitly out of scope (see Non-Goals).

### DI wiring: `jetStream.CreateObjectStoreContext()` singleton, registered next to `kv`
`Program.cs`: `var obj = jetStream.CreateObjectStoreContext();` right after the existing `var kv =
jetStream.CreateKeyValueStoreContext();` line, then `services.AddSingleton(obj);` — identical
pattern. `MainWindow.cs` resolves `INatsObjContext` the same way it resolves `INatsKVContext`,
constructs `ObjTab`, adds it to `ManagementTabs` after `kvTab`, and wires an `Alt+D5` shortcut +
status-bar relay identically to `kvTabShortcut`/`kvStatusShortcut`.

## Risks / Trade-offs

- **[Confirmed, not a risk]** The `OBJ_<bucket>` stream-naming convention was verified live: `.bin/
  nats object add PROBEBUCKET` against the dev server reported `JetStream Stream: OBJ_PROBEBUCKET`,
  and `nats stream ls --all --json` listed it alongside `KV_LAZYNATS_DEMO`/`KV_saga-demo-state` —
  confirming both that the prefix convention holds and that the server's plain stream-list API
  (which `INatsJSContext.ListStreamsAsync()` wraps) returns KV/OBJ-backing streams same as any
  other; `nats`'s own CLI just hides them from its default (non-`--all`) listing client-side, the
  same client-side filtering `BucketName` already does for KV.
- **[Risk]** `GetInfoAsync` throwing rather than returning a non-throwing result means a
  not-found object and a genuine server error both surface as exceptions from the same call site;
  only `NatsObjNotFoundException` is treated as "vanished," so misclassifying an error as
  not-found (or vice versa) would either wrongly blank the panel or wrongly surface a status-bar
  error for a normal deletion race. → **Mitigation**: catch specifically
  `NatsObjNotFoundException`, not the broader `NatsObjException`, so only the documented
  not-found case is swallowed.
- **[Trade-off]** Object list holds names only, so highlighting always costs a fresh
  `GetInfoAsync` round trip even though `ListAsync` already had the full metadata moments earlier.
  → Accepted for consistency with the KV tab's shape; see the "Object list" decision above.

## Open Questions

None outstanding — the `OBJ_` stream-naming convention was verified live against a real server
before this design was finalized (see Risks above).
