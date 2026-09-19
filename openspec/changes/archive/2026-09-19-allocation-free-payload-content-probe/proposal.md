## Why

`PayloadContentProbe.Classify` decodes every payload into a throwaway heap `string` just to scan
it for control characters and hand it to `JsonDocument.Parse` - the decoded string itself is
never returned to the caller. Malformed UTF-8 (a plausible shape for binary payloads like
protobuf) is detected by catching `DecoderFallbackException`, and invalid JSON is detected by
catching `JsonException` - both exception-based control flow on paths that are expected to fire
regularly (most non-text payloads). This is the code the three TODOs in
`PayloadContentProbe.cs`/`PayloadBinaryText.cs` were left against; the fix for all three turns out
to be the same restructuring rather than three separate changes.

## What Changes

- Replace the try/catch `StrictUtf8.GetString` well-formedness check with
  `System.Text.Unicode.Utf8.IsValid(ReadOnlySpan<byte>)` - no string allocation, no exception on
  the binary path.
- Replace the decoded-`char` control-character scan with a scan over the raw UTF-8 bytes
  (`SearchValues<byte>` for the C0/DEL single-byte controls, plus an explicit `0xC2` + `0x80..0x9F`
  pair check for the C1 range) - verified byte-for-byte equivalent to the current `char.IsControl`
  behavior (UTF-8's byte-value ranges for ASCII/continuation/lead bytes never overlap once
  well-formedness is already established, so a raw-byte scan can't misidentify a byte inside a
  multi-byte sequence as a standalone control character).
- Replace the try/catch `JsonDocument.Parse(string)` JSON check with try/catch
  `JsonDocument.Parse(ReadOnlyMemory<byte>)` reading the UTF-8 bytes directly - no intermediate
  string, no redundant re-encode of already-UTF-8 bytes. The `try/catch (JsonException)` itself
  stays: `JsonDocument.Parse` throws rather than returning `false` for structurally invalid JSON,
  and the BCL leaves no non-throwing way to detect this (`TryParseValue` was tried first per the
  TODO's own suggestion, but confirmed empirically during implementation to throw for exactly the
  same cases while also requiring a hand-rolled trailing-content check `Parse` already does
  internally - strictly worse, not an improvement) - the allocation goal is fully met here even
  though the exception-avoidance goal isn't.
- Update the stale `// TODO: this code needs some performance tuning as it will be executed on
  every keystroke / message received` header comment, which predates the caching
  (`FeedEnvelope.CachedContentKind`) and debounced/gated polling (`PollingDetailsView`) that now
  bound how often `Classify` actually runs.

No observable classification behavior changes - this is an internal implementation swap, verified
against every scenario in `openspec/specs/payload-content-probe/spec.md` plus the full C0/C1
control-character range and UTF-8 boundary cases (NBSP, other `0xC2`-led characters, emoji) by a
standalone comparison script.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `payload-content-probe`: gains a new requirement stating that classification runs without
  heap-allocating a decoded string proportional to payload length, and without exception-based
  control flow for the well-formed-UTF-8 check (the JSON-parse check still catches `JsonException`
  internally - the BCL's `JsonDocument.Parse` has no non-throwing way to detect invalid JSON
  syntax) - an internal-but-verifiable architectural guarantee, not a change to what classifies as
  `Json`/`Utf8Text`/`Binary`,
  mirroring how `payload-types`' `hex-base64-allocation-free-validation` change stated its own
  no-proportional-allocation guarantee as a requirement. All of that capability's existing
  classification requirements (JSON/text/binary distinction, strict UTF-8, C0/C1 control-character
  handling) are unchanged.

## Impact

- `src/lazynats.Core/Payloads/PayloadContentProbe.cs` - full rewrite of `Classify`/`IsJson`,
  same public signature (`Classify(byte[] payload) -> PayloadContentKind`).
- No caller changes needed (`PayloadDetailSection`, `FeedRowFormatter`, `MessageDetailDialog`,
  `ValuePeek` all consume only the returned `PayloadContentKind`).
- No new dependencies - `System.Text.Unicode.Utf8`, `SearchValues<byte>`, and
  `JsonDocument.Parse(ReadOnlyMemory<byte>)` are all in the BCL on `net10.0` and AOT/trim-safe.
