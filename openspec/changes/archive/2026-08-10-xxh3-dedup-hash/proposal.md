## Why

`MessageDeduplicator.ComputeKey` builds its dedup key with `System.HashCode`, including a
`foreach (var b in data) hash.Add(b)` loop over every payload byte individually — no
vectorization, one call per byte, and the resulting 32-bit key is a known accepted collision
risk (`design.md`'s "Hash collisions" note in the archived `add-subscriptions-feed` change). On
any payload of real size (JSON, binary blobs) this is measurably wasteful CPU on a path that
runs once per received NATS message, and the 32-bit width makes an already-accepted risk worse
than it needs to be.

## What Changes

- Replace the `HashCode`-based key in `MessageDeduplicator.ComputeKey` with
  `System.IO.Hashing.XxHash3` (64-bit, SIMD-accelerated), fed via its incremental `Append`
  API (subject, then each header key/value, then payload) instead of per-byte/per-field
  `HashCode.Add`.
- A fresh `XxHash3` instance is created per `ComputeKey` call (not reused/`Reset()`), avoiding
  any hidden lifecycle/exception-safety hazard from stale hasher state — the allocation cost is
  negligible next to the per-byte loop it replaces.
- Subject and header strings are hashed via `MemoryMarshal.AsBytes` over their UTF-16 char span
  (a zero-copy reinterpret cast) rather than `Encoding.UTF8.GetBytes`, since the key only needs
  to be internally consistent, not a real UTF-8 encoding. Header values are appended directly
  from `StringValues`' individual strings, skipping the allocation `StringValues.ToString()`
  would otherwise do to join multi-valued headers.
- `_lastSeen`'s key type changes from `Dictionary<int, DateTimeOffset>` to
  `Dictionary<ulong, DateTimeOffset>`.
- Add `System.IO.Hashing` as a package reference to `lazynats`, plus a new probe
  (`Probes/XxHash3Probe.cs`) in `lazynats.AotProbe` exercising the incremental `Append` /
  `GetCurrentHashAsUInt64` path, per the existing "new library gets its own probe" convention.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: the dedup key requirement is widened from an implicit, unstated 32-bit width to
  an explicit 64-bit commitment, shrinking (not eliminating) the already-accepted collision
  risk. (`aot-probe`'s existing "Isolated Per-Library Probes" requirement already covers adding
  a new probe file without a requirement change, so it is not listed as modified.)

## Impact

- `src/lazynats/LiveFeed/MessageDeduplicator.cs` — `ComputeKey` rewritten, `_lastSeen` key type
  changed.
- `src/lazynats/lazynats.csproj` — new `System.IO.Hashing` package reference.
- `src/lazynats.AotProbe/Probes/XxHash3Probe.cs` (new) and its `Program.cs` registration.
- No public/observable behavior change: dedup semantics (window, included/excluded fields) are
  unchanged, only the hash algorithm and its allocation profile.
