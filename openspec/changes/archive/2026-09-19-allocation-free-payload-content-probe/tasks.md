## 1. Rewrite `PayloadContentProbe.Classify`

- [x] 1.1 Replace the header TODO comment with one describing the actual call-frequency
      architecture (cached-per-envelope on the live feed via `FeedEnvelope.CachedContentKind`,
      poll-gated via `PollingDetailsView`/`ValuePeek`, once-per-dialog-open via
      `PayloadDetailSection`) instead of the stale "every keystroke / message received" framing.
- [x] 1.2 Replace the try/catch `StrictUtf8.GetString(payload)` well-formedness check with
      `System.Text.Unicode.Utf8.IsValid(payload)` - return `Binary` immediately on `false`, no
      string produced, no exception. Remove the now-unused `StrictUtf8` field and its
      `DecoderFallbackException` catch.
- [x] 1.3 Add a `private static readonly SearchValues<byte> BadC0Bytes` field (built once) covering
      `0x00-0x08, 0x0B-0x0C, 0x0E-0x1F, 0x7F` (C0 controls minus tab/LF/CR, plus DEL).
- [x] 1.4 Replace the decoded-`char` control-character loop with a raw-byte scan over `payload`:
      `IndexOfAny(BadC0Bytes)` for the single-byte case, plus an explicit forward scan for the
      `0xC2` + `0x80-0x9F` byte pair (C1 controls `U+0080-U+009F`) - return `Binary` if either
      matches. Inline comment documenting why this is exact once `Utf8.IsValid` has already passed
      (ASCII/continuation/lead byte ranges never overlap in well-formed UTF-8).
- [x] 1.5 Replace `IsJson(string)`'s try/catch `JsonDocument.Parse(text)` with try/catch
      `JsonDocument.Parse(payload)` over the `ReadOnlyMemory<byte>` overload - no string decode, and
      the "single JSON value, optionally surrounded by whitespace" trailing-content rule is enforced
      natively by `Parse` itself, with no hand-rolled check needed. (A `Utf8JsonReader`/
      `TryParseValue`-based version was tried first per the TODO's own suggestion, but confirmed
      empirically to throw for the same invalid-JSON cases as `Parse` while additionally requiring a
      hand-rolled trailing-content check `Parse` already does - see design.md Decision 3.)
- [x] 1.6 Confirm `Classify`'s public signature (`Classify(byte[] payload) -> PayloadContentKind`)
      and every existing call site (`PayloadDetailSection`, `FeedRowFormatter`,
      `MessageDetailDialog`, `ValuePeek`) are unchanged - no caller edits needed.

## 2. Verification

- [x] 2.1 Add `lazynats.Core.Tests` coverage for every scenario in
      `openspec/specs/payload-content-probe/spec.md`: valid JSON → `Json`, non-JSON UTF-8 text →
      `Utf8Text`, malformed UTF-8 → `Binary`, empty payload → `Utf8Text`, emoji/non-ASCII text →
      `Json`/`Utf8Text` (never `Binary`), malformed-but-mostly-ASCII → `Binary`, well-formed UTF-8
      with an embedded control character (not tab/LF/CR) → `Binary`, well-formed UTF-8 using only
      tab/LF/CR as control chars → `Json`/`Utf8Text`.
- [x] 2.2 Add a case sweeping the full C1 range (`U+0080-U+009F`), both standalone and embedded mid-
      string, asserting `Binary`, plus boundary non-controls that must NOT classify as `Binary`
      (`U+00A0` NBSP, another `0xC2`-led character like `U+0104`, an emoji, plain ASCII, tab/LF/CR) -
      mirrors the interactive verification already done for this change.
- [x] 2.3 Add a case for the trailing-content rule: a payload like `{"a":1} garbage` (valid JSON
      value followed by non-whitespace) must classify as `Utf8Text`, not `Json`.
- [x] 2.4 Add a case asserting `Classify` on a large binary (non-UTF-8) payload does not allocate a
      decoded string proportional to payload length, via
      `GC.GetAllocatedBytesForCurrentThread()` before/after a warmed-up call (mirrors
      `IsValidHex_DoesNotAllocateProportionallyToPayloadLength` from the hex/base64
      allocation-free-validation change).
- [x] 2.5 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 2.6 `dotnet test src/lazynats.Core.Tests/lazynats.Core.Tests.csproj` passes.
