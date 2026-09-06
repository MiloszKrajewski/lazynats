## Why

The Stream, Consumer, KV bucket, and OBJ bucket detail panels print several JetStream limit
fields verbatim (e.g. `Max Age: 00:00:00`, `Max Bytes: -1`, `Max Deliver: -1`). These are NATS
server sentinel values meaning "no limit is enforced," not literal zero-second ages or negative
byte counts, but the UI shows the raw number/`TimeSpan`, forcing the user to already know NATS's
sentinel conventions to read their own stream/consumer/bucket configuration correctly.

## What Changes

- Format sentinel values on detail panels as a bracketed human-readable label instead of the raw
  number/duration, for every currently-displayed field that carries one:
  - Stream Detail Panel: Max Messages (`-1`), Max Bytes (`-1`), Max Age (`00:00:00`) → `(unlimited)`.
  - Consumer Detail Panel: Max Deliver (`-1`), Max Ack Pending (`-1`) → `(unlimited)`.
  - KV Bucket Detail Panel: History (`-1`), Max Age (`00:00:00`) → `(unlimited)`.
  - OBJ Bucket Detail Panel: Max Age (`00:00:00`) → `(unlimited)`.
- Add a small shared formatting helper (count-sentinel and duration-sentinel variants) used by all
  four `BuildRows` implementations instead of each view re-deriving the same ternary, so the
  sentinel-to-label mapping lives in one place.
- Leave already-translated fields alone: KV's TTL Limit and the various `(none)` placeholders
  (Subjects, Filter Subject, Description, Digest) already read correctly and are out of scope.
- Bring the KV Bucket Detail Panel requirement's field enumeration in line with what the panel has
  actually shown since `BucketDetails.cs` added the row (it currently omits "Max Age").

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-streams`: Stream Detail Panel and Consumer Detail Panel requirements gain scenarios
  specifying that unlimited-sentinel limit fields render as `(unlimited)` rather than their raw
  server value.
- `nats-kv`: Bucket Detail Panel requirement's field list gains Max Age and gains scenarios
  specifying `(unlimited)` rendering for History and Max Age.
- `nats-obj`: Bucket Detail Panel requirement gains a scenario specifying `(unlimited)` rendering
  for Max Age.

## Impact

- `src/lazynats/Streams/StreamDetails.cs`, `src/lazynats/Streams/ConsumerDetails.cs`,
  `src/lazynats/KVStore/BucketDetails.cs`, `src/lazynats/ObjStore/BucketDetails.cs` — reformat the
  affected rows.
- A new small shared helper (exact location decided in design.md) used by the four files above.
- No server-side or wire-protocol changes; purely a display-formatting fix. No new dependencies.
