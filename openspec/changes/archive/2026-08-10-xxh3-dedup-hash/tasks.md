## 1. Dependency and AOT probe

- [x] 1.1 Add `System.IO.Hashing` package reference to `src/lazynats/lazynats.csproj`.
- [x] 1.2 Add `Probes/XxHash3Probe.cs` to `lazynats.AotProbe`, exercising the incremental
      `Append`/`GetCurrentHashAsUInt64` path (multiple `Append` calls, not just construction),
      following the existing probe files' shape (e.g. the Rx probe) as a template.
- [x] 1.3 Register the new probe in `lazynats.AotProbe`'s `Program.cs` alongside the existing
      NATS/Rx/DI probes.
- [x] 1.4 Run `test-aot.ps1` from `src/lazynats.AotProbe` and confirm the new probe passes
      under `PublishAot`.

## 2. MessageDeduplicator rewrite

- [x] 2.1 Change `_lastSeen` from `Dictionary<int, DateTimeOffset>` to
      `Dictionary<ulong, DateTimeOffset>` in `src/lazynats/LiveFeed/MessageDeduplicator.cs`,
      and update `Prune`'s expired-key list type to match.
- [x] 2.2 Rewrite `ComputeKey` to build a `System.IO.Hashing.XxHash3` instance (new per call)
      and return `GetCurrentHashAsUInt64()`:
      - `Append(MemoryMarshal.AsBytes(message.Subject.AsSpan()))` for the subject.
      - For each header key/value pair, `Append` the key's char span as bytes, then `Append`
        each string in the header's `StringValues` individually (no `.ToString()` join).
      - `Append(data)` for the payload when `message.Data` is non-null (`byte[]` converts to
        `ReadOnlySpan<byte>` implicitly).
- [x] 2.3 Update `IsDuplicate`'s `key` local's inferred type usage if needed (should follow
      automatically from `ComputeKey`'s new `ulong` return type).

## 3. Verification

- [x] 3.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 3.2 Manually exercise duplicate collapsing: run `lazynats` against a NATS server, add two
      overlapping subscriptions (e.g. `invoices.>` and `invoices.get.*`), publish a message
      matching both, and confirm the feed still shows exactly one row (per
      `live-feed` spec's "same message ... appears once" scenario).
- [x] 3.3 Confirm distinct messages published after the dedup window still appear as separate
      rows (regression check against the "not collapsed" scenario).
