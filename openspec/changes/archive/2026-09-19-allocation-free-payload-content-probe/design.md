## Context

`PayloadContentProbe.Classify(byte[] payload)` classifies raw message-payload bytes into `Json`,
`Utf8Text`, or `Binary` for display purposes (see `openspec/specs/payload-content-probe/spec.md`).
The current implementation:

1. Strictly decodes the payload to a `string` via `new UTF8Encoding(false, true).GetString(payload)`,
   catching `DecoderFallbackException` to detect malformed UTF-8 → `Binary`.
2. Scans the decoded `string`'s `char`s for control characters (other than tab/LF/CR) → `Binary`.
3. Calls `JsonDocument.Parse(text)`, catching `JsonException` → `Utf8Text` on failure, `Json` on
   success.

The decoded string from step 1 is discarded after step 2/3 - it's never returned to the caller.
Callers only ever receive the `PayloadContentKind` enum (`PayloadDetailSection`,
`FeedRowFormatter`, `MessageDetailDialog`, `ValuePeek` - see proposal's Impact section).

Actual call frequency turns out lower than the file's own header TODO implies ("every keystroke /
message received"): `FeedEnvelope.CachedContentKind` caches the result per envelope, computed only
on first render of a row scrolled into view (not on receipt), and `ValuePeek`'s only caller
(`KeyDetails`, a `PollingDetailsView` subclass) runs on a 3s poll timer or a 100ms debounced
target-switch, gated to the single selected KV key. `PayloadDetailSection` classifies once at
dialog construction. So this isn't a true hot path in the per-keystroke sense - the case for this
change is allocation/exception hygiene on a path that still runs at least once per distinct
payload shown, not urgency from a measured bottleneck.

## Goals / Non-Goals

**Goals:**
- Eliminate the per-call string allocation - classification decides `Json`/`Utf8Text`/`Binary`
  without ever materializing a decoded `string`.
- Eliminate exception-based control flow for the malformed-UTF-8 case - an expected, common
  outcome (most non-text payloads are binary), not an exceptional one. (The invalid-JSON case
  turns out not to admit a non-throwing option in the BCL's public surface - see Decision 3's
  "Correction after implementation".)
- Byte-for-byte identical classification results to the current implementation for every input -
  this is an internal rewrite, not a behavior change.
- Resolve all three TODOs in `PayloadContentProbe.cs` (stackalloc/pool, SearchValues, TryParseValue)
  with one coherent design rather than three independent patches.

**Non-Goals:**
- Eliminating the `JsonDocument` DOM allocation entirely (e.g. hand-driving `Utf8JsonReader` with
  `TrySkip()`/`BytesConsumed` bookkeeping instead of `JsonDocument.TryParseValue`). Classification
  is still bounded to at most once per distinct payload shown (see Context), so the remaining
  `JsonDocument` allocation on the JSON/near-JSON path is a bounded, one-time cost per call, not
  worth the extra edge-case surface (trailing content, multiple top-level values) a hand-rolled
  reader would need to replicate `JsonDocument.Parse`'s "whole input is exactly one JSON value"
  rule correctly.
- Changing what any payload classifies as. The spec's existing scenarios are the acceptance test.
- Touching `PayloadBinaryText.IsValidHex`/`IsValidBase64`'s own separate TODO (different file,
  different concern - Hex/Base64 text validity, not payload content classification).
- Correcting the stale "every keystroke" framing beyond fixing the comment text itself - not
  re-deriving the actual call-frequency architecture (caching, polling) documented above, which is
  unchanged by this design.

## Decisions

### Decision 1: `System.Text.Unicode.Utf8.IsValid` replaces the try/catch strict decode

`Utf8.IsValid(ReadOnlySpan<byte> value)` (BCL, .NET 8+, available on this project's `net10.0`
target) reports strict UTF-8 well-formedness directly over the byte span - no output buffer, no
exception. This is a direct swap for step 1's `try { StrictUtf8.GetString(payload) } catch
(DecoderFallbackException)`, with the same strictness (rejects overlong/malformed sequences, not a
lossy best-effort decode) the current `UTF8Encoding(false, true)` was deliberately configured for.

**Alternative considered:** stack-allocate/pool a `char` buffer and decode with
`Encoding.UTF8.TryGetChars` in a loop, checking for decoder errors manually (closer to the
original TODO's literal wording - "stackalloc/Span for small payloads, pool byte buffer
otherwise"). Rejected: still produces a decoded buffer nothing downstream needs, and still needs
the buffer to be at least as large as the input in the worst case (mostly-ASCII payloads decode
~1:1), so pooling only avoids GC pressure, not the fundamental "decode something we then throw
away" waste. `Utf8.IsValid` sidesteps the need for any output buffer at all.

### Decision 2: Control-character scan moves to raw bytes, not decoded `char`s

Once `Utf8.IsValid` has confirmed the span is well-formed UTF-8, UTF-8's byte-value ranges for
ASCII (`0x00-0x7F`), continuation bytes (`0x80-0xBF`), and lead bytes (`0xC2-0xF4`) never overlap -
a byte's role is fully determined by its own value. This means:

- C0 controls (`U+0000-U+001F` minus tab/LF/CR) and DEL (`U+007F`) are single ASCII bytes that can
  only ever appear as standalone characters in well-formed UTF-8, never as a byte inside a
  multi-byte sequence - so a raw-byte scan for these values is exact, not approximate.
- C1 controls (`U+0080-U+009F`) always encode as the two-byte sequence `0xC2` followed by
  `0x80-0x9F` - so these need one explicit two-byte pair check alongside the single-byte scan
  (`0xC2` followed by anything `0xA0` or above, e.g. NBSP `U+00A0` or `Ą` `U+0104`, is not a
  control character and must not match).

Verified empirically (see `Verification` below) against all 32 C1 codepoints (alone and embedded),
the full C0 range, DEL, and boundary non-controls (NBSP, `U+0104`, an emoji, tab/LF/CR, plain
ASCII) - zero mismatches against the current `char.IsControl`-based reference.

The single-byte set (`0x00-0x08, 0x0B-0x0C, 0x0E-0x1F, 0x7F`) is built once via
`SearchValues<byte>.Create(...)` in a `static readonly` field, per the TODO's own suggestion, and
scanned with `ReadOnlySpan<byte>.IndexOfAny`. The C1 pair check is a plain forward scan for `0xC2`
followed by a range check on the next byte.

**Alternative considered:** keep decoding to `string` just for this step, since `SearchValues<char>`
would also work on decoded chars. Rejected: it would still pay the decode allocation, defeating
Decision 1.

### Decision 3: try/catch `JsonDocument.Parse(ReadOnlyMemory<byte>)` replaces try/catch `JsonDocument.Parse(string)`

`JsonDocument.Parse` has a `ReadOnlyMemory<byte>` overload that parses UTF-8 bytes directly, so
this removes the redundant re-encode-to-UTF8 that the `string` overload does internally (the
payload is already UTF-8 bytes; parsing the decoded `string` version means transcoding back) - the
allocation goal (no decoded string, ever) fully holds for this step. (No `ReadOnlySpan<byte>`
overload exists: `JsonDocument` retains the buffer for its lifetime as a lazily-materialized DOM,
which a stack-only `Span` can't back safely - `ReadOnlyMemory<byte>` is the byte-based option.)
Like the `string` overload, it natively enforces "exactly one JSON value, optionally surrounded by
whitespace" and throws `JsonException` otherwise - success → `Json`; exception → `Utf8Text`.

**Two rounds of correction after implementation, both from things only surfaced by writing and
running the code, not from reasoning about the API alone:**

1. The original plan here was `JsonDocument.TryParseValue(ref Utf8JsonReader, out JsonDocument?)`,
   per the TODO's own suggestion ("TryParseValue might save us from exception handling"). Verified
   empirically to be wrong: despite its "Try" name, `TryParseValue` does *not* avoid exceptions for
   structurally invalid JSON - it throws `JsonReaderException` for inputs like plain text
   (`"hello world"` → `'h' is an invalid start of a value`) or empty input (`"The input does not
   contain any JSON tokens"`), which is exactly the common "non-JSON text" case this change most
   wanted to make exception-free. The `Try` in its name reflects only *incomplete* data across
   multi-segment `IBufferReader` reads (more data might arrive), not *invalid* syntax - the public
   `Utf8JsonReader`/`JsonDocument` surface has no non-throwing way to detect malformed JSON, since
   `Read()` itself throws for syntax errors by design.
2. Given the exception is unavoidable either way, `TryParseValue` turned out to have no advantage
   over plain `Parse` while adding real cost: it still allocates the same `JsonDocument`, and
   because it only consumes the single parsed value (not the whole input), it additionally required
   hand-rolling a trailing-content check (`reader.BytesConsumed`, a `SearchValues<byte>` whitespace
   scan) to reject payloads like `{"a":1} garbage` - logic `Parse` already implements internally
   and correctly. Switching to `Parse(ReadOnlyMemory<byte>)` deleted that hand-rolled check
   entirely along with the `Utf8JsonReader` plumbing, for the same allocation/exception profile.

`openspec/specs/payload-content-probe/spec.md`'s requirement and "Non-JSON text" scenario reflect
the final guarantee: "no proportional allocation" throughout the whole `Classify` call, and "no
exception" only for the UTF-8-malformed path where `Utf8.IsValid` genuinely delivers it - not for
the JSON-invalid path, where a caught `JsonException` remains the only option the BCL offers.

### Decision 4: Update the stale header comment

`PayloadContentProbe.cs`'s header TODO ("this code needs some performance tuning as it will be
executed on every keystroke / message received") predates the caching/polling architecture
described in Context and overstates today's call frequency. Replace it with a comment describing
the actual, current call pattern (cached-per-envelope on the live feed, poll-gated on the KV value
peek, once-per-dialog-open elsewhere) so a future reader isn't misled into believing this runs on
literal keystrokes.

## Risks / Trade-offs

- [Risk] Hand-rolled byte-range logic for control-character detection is less obviously correct at
  a glance than `char.IsControl` on decoded text, even though it's proven exact. → Mitigation:
  keep the UTF-8 byte-range comment inline (why ASCII/continuation/lead ranges can't overlap once
  well-formedness is established) so the invariant is documented at the point of use, not just in
  this design doc; add unit tests in `lazynats.Core.Tests` covering the full C0/C1 sweep and
  boundary non-controls (the same cases the verification script checked).
- [Trade-off] `JsonDocument.Parse` still allocates a `JsonDocument` DOM on the JSON/parses-as-JSON
  path - not fully allocation-free end-to-end. Accepted per Non-Goals: this is a bounded,
  once-per-shown-payload cost, and a fully allocation-free reader-driven alternative isn't worth
  its extra edge-case surface here.
- [Trade-off, discovered during implementation] The invalid-JSON case still uses `try/catch
  (JsonException)` around `Parse` - see Decision 3's corrections. Accepted: the allocation goal (no
  decoded string) fully holds for this step regardless; only the exception-avoidance goal turned
  out unreachable here with the BCL's public API, not something this change's design choices could
  have avoided. (Decision 3's second correction also removed a self-inflicted risk: an earlier
  `TryParseValue`-based draft required a hand-rolled trailing-content check that `Parse` renders
  unnecessary, so there's no longer a "get the manual check subtly wrong" risk to mitigate.)

## Migration Plan

No migration - internal implementation swap behind the existing `Classify(byte[]) ->
PayloadContentKind` signature. No callers change. No feature flag needed; land as a single change
and verify via existing manual/automated coverage of the four call sites plus new unit tests.

## Open Questions

None - the design was validated interactively (byte-range reasoning + an empirical comparison
script covering the full C1 range, C0 range, DEL, and UTF-8 boundary non-controls) before writing
this document.
