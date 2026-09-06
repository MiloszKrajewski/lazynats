## 1. Shared formatting helper

- [x] 1.1 Add `src/lazynats/Components/LimitFormat.cs` with `Count(long value)` (`< 0` →
      `(unlimited)`) and `Duration(TimeSpan value)` (`<= TimeSpan.Zero` → `(unlimited)`), per
      design.md's "Decisions" section.

## 2. Stream Detail Panel

- [x] 2.1 In `src/lazynats/Streams/StreamDetails.cs`, use `LimitFormat.Count` for Max Messages
      and Max Bytes, and `LimitFormat.Duration` for Max Age.

## 3. Consumer Detail Panel

- [x] 3.1 In `src/lazynats/Streams/ConsumerDetails.cs`, use `LimitFormat.Count` for Max Deliver
      and Max Ack Pending.

## 4. KV Bucket Detail Panel

- [x] 4.1 In `src/lazynats/KVStore/BucketDetails.cs`, use `LimitFormat.Count` for History
      (`MaxMsgsPerSubject`) and `LimitFormat.Duration` for Max Age. Leave the existing TTL Limit
      ternary as-is (different semantics — see design.md's Context section).

## 5. OBJ Bucket Detail Panel

- [x] 5.1 In `src/lazynats/ObjStore/BucketDetails.cs`, use `LimitFormat.Duration` for Max Age.

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 6.2 Against a real NATS server (`nats://localhost:4222`), create a stream/consumer/KV
      bucket/OBJ bucket with unlimited Max Age (and, for stream/consumer, unlimited Max
      Messages/Max Bytes/Max Deliver/Max Ack Pending via the `nats` CLI in `.bin/` where the
      app's own create dialogs don't expose the field), then confirm via `tmux` (per CLAUDE.md's
      "Build / run" section) that each affected row now reads `(unlimited)` instead of a raw
      sentinel, and that a non-unlimited value on the same field still renders as before.
- [x] 6.3 Confirm KV's TTL Limit row and all pre-existing `(none)` rows (Subjects, Filter Subject,
      Description, Digest) are unchanged.
