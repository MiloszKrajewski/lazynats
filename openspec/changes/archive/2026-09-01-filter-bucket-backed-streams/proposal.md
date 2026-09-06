## Why

The Streams tab lists every JetStream stream, including the streams that back KV buckets
(`KV_<name>`) and Object Store buckets (`OBJ_<name>`) — both already have their own dedicated tabs
(`3:Values`, `4:Objects`). Seeing the same bucket twice, once correctly in its own tab and once as
raw stream plumbing in Streams, is noise the user has to filter out mentally every time. Separately,
today's bucket-vs-plain-stream classification (used by the Values/Objects tabs to decide which
streams are buckets at all) is name-prefix-only, so a plain stream someone happens to name
`KV_orders` is misclassified as a KV bucket — worth tightening while touching this logic.

## What Changes

- Streams tab excludes any stream that's actually backing a KV or Object Store bucket, so
  `2:Streams` only shows genuine, user-created streams.
- Bucket classification (used by Streams to exclude, and by Values/Objects to decide what counts
  as a bucket) is tightened from name-prefix-only to name-prefix **and** a matching reserved
  subject (`$KV.<name>.`/`$O.<name>.`) — a plain stream that merely happens to be named
  `KV_<name>`/`OBJ_<name>` without the matching subject binding is no longer misclassified as a
  bucket.
- The classification logic (currently duplicated per-tab in `Values/BucketName.cs` and
  `Objects/BucketName.cs`) is consolidated into one shared helper, now with a third consumer
  (Streams).
- Values/Objects bucket lists carry the derived bare bucket name on each list item instead of
  re-deriving it from the full stream name on every list operation (sort, filter, render).

## Capabilities

### New Capabilities
(none — this is shared classification logic backing the three modified capabilities below, not a
user-facing capability of its own)

### Modified Capabilities
- `nats-streams`: the "Stream List" requirement now excludes KV/Object Store bucket-backing
  streams from what it lists.
- `nats-kv`: the "Bucket List" requirement's definition of "bucket" is tightened — a stream must
  match both the `KV_` name prefix and a subject rooted at `$KV.<name>.` to count.
- `nats-obj`: the "Bucket List" requirement's definition of "bucket" is tightened the same way,
  for `OBJ_`/`$O.<name>.`.

## Impact

- `src/lazynats/Streams/StreamsTab.cs` — `RefreshListAsync` gains a filter step.
- `src/lazynats/Values/BucketName.cs`, `src/lazynats/Objects/BucketName.cs` — removed, replaced by
  one shared helper (exact location decided in design.md).
- `src/lazynats/Values/BucketListView.cs`, `BucketNamePresenter.cs`, `BucketDetails.cs`,
  `ValuesTab.cs` — switch from `NatsKVStatus` to a small wrapper item carrying the derived name.
- `src/lazynats/Objects/BucketListView.cs`, `BucketNamePresenter.cs`, `BucketDetails.cs`,
  `ObjectsTab.cs` — same, wrapping `StreamInfo`.
- No server-side or protocol changes; purely client-side classification and list-population logic.
