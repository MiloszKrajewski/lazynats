## Context

The Streams tab (`src/lazynats/Streams/`) already established the pattern this change reuses: a
two-level LHS list / RHS details tab built from `DrillableListView<T>` (list mechanics: presenter
rendering, empty hint, identity-preserving refresh, Ctrl+R) and `PollingDetailsView<TTarget,TInfo>`
(details mechanics: label:value row rendering, active-gated lazy poll pipeline, error reporting).
Both base classes live in `src/lazynats/Components/` specifically so a KV/OBJ tab could reuse them
(see their file-header comments). `openspec/specs/nats-streams/spec.md`,
`openspec/specs/drillable-list/spec.md`, and `openspec/specs/polling-details/spec.md` describe the
existing behavior in full; this design only calls out where KV needs something new.

The `NATS.Client.KeyValueStore` package (v2.8.2) is already referenced in `lazynats.csproj` but
unused. Relevant surface, confirmed against the installed package's XML docs and via reflection
(not yet in `context7`'s indexed docs beyond the quick-start snippets):

- `jetStream.CreateKeyValueStoreContext()` (sync, `NATS.Net.NatsClientExtensions`, mirrors
  `connection.CreateJetStreamContext()`) → `INatsKVContext`.
- `INatsKVContext.GetStatusesAsync()` → `IAsyncEnumerable<NatsKVStatus>`, where
  `NatsKVStatus { string Bucket, bool IsCompressed, TimeSpan LimitMarkerTTL, StreamInfo Info }` — a
  bucket is a stream under the hood, so `Info.State`/`Info.Config` carry the same shape
  `StreamDetails` already reads.
- `INatsKVContext.GetStoreAsync(bucket)` → `INatsKVStore`, needed to reach key-level operations for
  a specific bucket.
- `INatsKVStore.GetKeysAsync()` → `IAsyncEnumerable<string>` (bare key names, no metadata).
- `INatsKVStore.TryGetEntryAsync<byte[]>(key)` → `NatsResult<NatsKVEntry<byte[]>>`, non-throwing.
  `NatsKVEntry<byte[]> { byte[] Value, ulong Revision, DateTimeOffset Created, NatsKVOperation
  Operation }`.

## Goals / Non-Goals

**Goals:**
- Ship the `4:KV` tab per `doc/UI.md`: bucket list with stats, drill into a bucket's keys, key
  metadata + value detail.
- Reuse `DrillableListView`/`PollingDetailsView` as-is at the list level and bucket-details level —
  no changes needed there.
- Extend `PollingDetailsView` minimally (one optional body region) so key-details' "value takes all
  available space" need doesn't require a parallel, hand-rolled poll pipeline.

**Non-Goals:**
- No mutation (create/edit/delete/purge) for buckets or keys — read-only, matching Streams.
- No JSON/UTF8-aware smart rendering of the value — plain UTF-8 decode only, same simplicity level
  as `FeedRowFormatter` today. Smarter payload rendering is a separate future change that would
  benefit the live feed too.
- No scrolling for the value body — clipped to whatever height is available. A large value just
  shows its first N visible lines/columns.
- No distinct "key was deleted" indicator — a key that's vanished by poll time (deleted, purged, or
  never existed) renders identically to nothing being highlighted: a blank details panel.

## Decisions

### Extend `PollingDetailsView` with an optional body region, not a parallel component
Key details needs a small metadata header (revision/created/operation) plus the value filling the
rest of the pane — `PollingDetailsView`'s current `OnDrawingContent` only knows how to draw uniform
label:value rows top-to-bottom. Rather than build `KeyDetails` as a standalone view that
re-implements the active-gated lazy-poll pipeline (`SetActive`/`SetTarget`/`Show`/error-reporting)
from scratch, add one new overridable hook to the shared base:

```csharp
protected virtual string? BuildBody(TInfo info) => null;
```

`OnDrawingContent` renders the existing header rows first, then — if `BuildBody` returns non-null —
draws the body text starting one row below (blank separator) and filling `Height` - (header rows +
1), clipped to available width/height, no scrolling. Rows.Length == 0 collapses correctly (no
header, body starts at row 0) so a subclass could in principle be body-only, though KV always has a
small header. Default implementation returns `null`, so `StreamDetails`/`ConsumerDetails` render
exactly as before — zero behavior change for existing subclasses.

**Alternative considered**: a standalone `KeyDetails : View` composing its own
`Observable.Interval`/`SelectAsync`/`ObserveOnApp` pipeline (copying `PollingDetailsView`'s
internals). Rejected — it would duplicate the active-gating, lazy-start, and error-reporting logic
that `polling-details`/`reactive-polling` already got right, for no benefit; the only real
difference KV needs is the rendering shape, not the pipeline.

### Deleted/missing key: reuse `Show(null)`, add no wrapper type
`FetchAsync` for key details calls `TryGetEntryAsync` (non-throwing) and returns `null` whenever
the result is an error (key not found, deleted, purged — no attempt to distinguish these cases).
`PollingDetailsView.Show(TInfo?)` already treats `null` as "clear the panel" (today used for "no
item highlighted"); this reuses that exact path for "highlighted key is gone" too. No "(deleted)"
rendering state, no behavioral distinction from "nothing highlighted" — explicitly simplified per
user direction to reduce scope.

`TInfo` for key details is `KeyDetails.Entry`, a small private record class projecting the fields
`BuildRows`/`BuildBody` need (Key, Revision, Created, Operation, Value), not `NatsKVEntry<byte[]>`
directly — `NatsKVEntry<T>` turned out to be a value type (record struct), and
`PollingDetailsView`'s unconstrained `TInfo?` return only erases consistently for a reference-type
`TInfo` (both existing subclasses, `StreamInfo`/`ConsumerInfo`, are classes); using the struct
directly as `TInfo` hits a base/override return-type mismatch (CS0508). `Entry` is purely that
struct-to-reference-type shim — it carries no "deleted" concept of its own.

### Bucket list filtering: `GetStatusesAsync()` returns every stream, not just KV buckets
Verified live against a real server (mixed KV buckets and plain JetStream streams): `INatsKVContext
.GetStatusesAsync()` (v2.8.2) iterates *every* stream on the server and wraps each one as a
`NatsKVStatus`, with `Bucket` set to the raw, unstripped stream name — not filtered to KV-backed
streams at all, contrary to its XML doc ("Gets the status for all buckets"). Confirmed two ways: a
plain non-KV stream (`orders`) appeared in the bucket list, and passing a real KV status's
`.Bucket` (e.g. `"KV_TESTBUCKET"`, already prefixed) into `GetStoreAsync` failed with "stream not
found" — `GetStoreAsync` expects the *bare* bucket name and prepends `KV_` itself.

Fix: `BucketName` (`src/lazynats/Kv/BucketName.cs`) applies the standard KV convention (the same
one the `nats` CLI and server use) client-side — a bucket is any stream named `KV_<name>`;
`IsKvStream` filters `KvTab.RefreshListAsync`'s results to those, and `From` strips the prefix to
get the bare name used everywhere downstream (display, identity, `SetTarget`, `GetStoreAsync`).
`NatsKVStatus.Bucket` itself is never read directly outside `BucketName`.

### Bucket-level detail fetch: reuse `GetStatusesAsync`'s per-bucket shape, not a raw `StreamInfo` fetch
`BucketDetails : PollingDetailsView<string, NatsKVStatus>` fetches via
`_kv.GetStoreAsync(bucket).GetStatusAsync()` on each poll tick (returns `NatsKVStatus` directly,
same shape as the list), rather than falling back to `_jetStream.GetStreamAsync("KV_" + bucket)`
(which would work, since a bucket is literally that stream, but hardcodes the `KV_` naming
convention the KV package already encapsulates — `INatsKVStore.GetStatusAsync()` is the sanctioned
way to get bucket status, so use it instead of reaching around it).

### Key-level list fetch: `GetStoreAsync(bucket).GetKeysAsync()`, fresh every descend
Mirrors `nats-streams`' "Consumer List" requirement (`ListConsumersAsync` re-fetched fresh on every
descend, never cached across an ascend/descend cycle) — same shape, applied to
`GetKeysAsync()` returning bare key names for the LHS list.

### DI wiring: resolve `INatsKVContext` once in `Program.cs`, register as singleton
`var kv = jetStream.CreateKeyValueStoreContext();` right after `var jetStream =
connection.CreateJetStreamContext();`, then `services.AddSingleton(kv);` — mirrors how `jetStream`
itself is already registered and consumed by `StreamsTab`.

### Row content
- Bucket details (`BucketDetails`, from `NatsKVStatus`): Bucket, Compressed, TTL Limit (from
  `LimitMarkerTTL`, omitted/shown as "(none)" when default), then a blank separator, then the same
  state shape `StreamDetails` already surfaces from `Info.State`/`Info.Config` relevant to a KV
  bucket: Keys (`State.Messages` — includes historical revisions, so label it accordingly rather
  than implying "live key count"), Bytes, History (`Config.MaxMsgsPerSubject`), Max Age.
- Key details header (`KeyDetails`, from `NatsKVEntry<byte[]>`): Key, Revision, Created, Operation,
  Size (`Value.Length` bytes). Body: UTF-8 decode of `Value` (empty string renders as an empty
  body, not an error — a `Del`/`Purge` operation's entry has an empty value, but per the
  non-goal above we don't special-case operation kind for display).

### Instant refresh on key highlight change: `PollingDetailsView.RefreshNow()`
Found via live dogfooding: for `StreamDetails`/`ConsumerDetails`/`BucketDetails`, highlighting a
new item shows its details instantly, because the paired list already carries the full info object
(`StreamInfo`/`ConsumerInfo`/`NatsKVStatus`) and the owning tab calls `Show(item)` synchronously
right after `SetTarget`. `KeyListView`'s list, by contrast, only carries bare key name strings (see
the "Key-level list fetch" decision above) — there's nothing to `Show()` synchronously, so
`KeyDetails` sat blank until the next ~3s poll tick, visibly inconsistent with every other level in
the app.

Fix: `PollingDetailsView` gains `RefreshNow()` — fetches the current target immediately, bypassing
both the active-gate and the poll interval, and applies the result via the same `Show()` path a
poll tick uses (including the same "null result isn't shown" behavior, so a fetch that comes back
with nothing doesn't fight with a synchronous `Show(null)` the caller already did). A staleness
guard (compare the fetched-for target against the current target when the fetch completes) handles
rapid highlight changes — an in-flight fetch for a key the user has since navigated away from is
dropped rather than overwriting the newer selection.

`KvTab.OnKeyHighlightChanged` now does `SetTarget` → `Show(null)` (clear, no stale flash) →
`RefreshNow()` (kick off the fetch immediately). This is additive to `PollingDetailsView` and
touches nothing in `StreamDetails`/`ConsumerDetails`/`BucketDetails`, which don't need it and don't
call it.

## Risks / Trade-offs

- **[Risk]** `State.Messages` on a KV bucket's underlying stream counts every historical revision,
  not distinct live keys — labeling this row "Keys" would be misleading. → **Mitigation**: label it
  something accurate (e.g. "Entries") in `BuildRows`, and don't claim it's a live-key count.
- **[Risk]** A very large value (e.g. a multi-KB JSON blob) is silently truncated by clipping with
  no visual indicator that more content exists off-screen. → **Mitigation**: acceptable for this
  pass per explicit user direction; a follow-up change can add scrolling once it's clear how often
  this actually matters in practice.
- **[Trade-off]** Treating "key deleted" identically to "nothing highlighted" means a user watching
  the details panel go blank can't tell whether they scrolled off the list or the key was actually
  deleted server-side. → Accepted trade-off per explicit user direction (reduce complexity now);
  revisit if it proves confusing in practice.

## Open Questions

None outstanding — scope, layout, and error-handling decisions above were confirmed during
exploration before this change was drafted.
