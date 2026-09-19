using System.Buffers;
using System.Text.Json;
using System.Text.Unicode;

namespace lazynats.Core.Payloads;

// Classification runs at most once per distinct payload shown, not on every keystroke or message
// received: cached per-envelope on the live feed (FeedEnvelope.CachedContentKind, computed on
// first render of a row scrolled into view), poll-timer/debounce-gated for the KV value peek
// (PollingDetailsView/ValuePeek, scoped to the single selected key), and once at dialog
// construction for PayloadDetailSection.

// Classifies raw payload bytes for display purposes - the inverse of the PayloadType/
// PayloadValidation/PayloadEncoding trio (which validate/encode already-chosen-type text for
// sending), so kept as its own component rather than folded into those. See design.md's "strict
// UTF-8 decoding + a printable-content check, then a JSON parse attempt" decision for why both a
// plain "looks like printable ASCII" heuristic and "any well-formed UTF-8 is text" were rejected.
internal static class PayloadContentProbe
{
    // C0 controls (U+0000-U+001F) minus tab/LF/CR, plus DEL (U+007F). In well-formed UTF-8 these
    // single-byte ASCII values can only ever appear as standalone characters, never as a byte
    // inside a multi-byte sequence (continuation bytes are 0x80-0xBF, lead bytes 0xC2-0xF4) - so a
    // raw-byte scan for these values, once Utf8.IsValid has confirmed well-formedness, is exact.
    private static readonly SearchValues<byte> BadC0Bytes = SearchValues.Create(
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x0B, 0x0C,
        0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
        0x7F);
    
    public static PayloadContentKind Classify(byte[] payload) => 
        Classify(payload.AsMemory());

    public static PayloadContentKind Classify(ReadOnlyMemory<byte> payload)
    {
        if (!Utf8.IsValid(payload.Span))
            return PayloadContentKind.Binary;

        // Binary data can, by chance, still be well-formed UTF-8 - reject it here if it carries
        // control characters a genuine text/JSON payload wouldn't, rather than rendering it as
        // "text" full of control-character noise. Tab/newline/carriage-return are the only control
        // characters a legitimate text payload uses, so those alone are exempt.
        if (HasDisallowedControlBytes(payload.Span))
            return PayloadContentKind.Binary;

        return IsJson(payload) ? PayloadContentKind.Json : PayloadContentKind.Utf8Text;
    }

    private static bool HasDisallowedControlBytes(ReadOnlySpan<byte> payload)
    {
        if (payload.IndexOfAny(BadC0Bytes) >= 0)
            return true;

        // C1 controls (U+0080-U+009F) always encode as the two-byte sequence 0xC2 followed by
        // 0x80-0x9F - 0xC2 followed by anything from 0xA0 up (e.g. NBSP U+00A0, Ą U+0104) is a
        // different, non-control character and must not match.
        while (true)
        {
            var index = payload.IndexOf((byte)0xC2);
            if (index < 0 || index + 1 >= payload.Length)
                return false;
            if (payload[index + 1] is >= 0x80 and <= 0x9F)
                return true;
            // Skip past the whole two-byte unit we just inspected (not just the 0xC2), rather than
            // re-scanning its continuation byte for another 0xC2 match on the next iteration.
            payload = payload[(index + 2)..];
        }
    }

    private static bool IsJson(ReadOnlyMemory<byte> payload)
    {
        // JsonDocument.Parse(ReadOnlyMemory<byte>) parses UTF-8 bytes directly - no string decode
        // (JsonDocument has no ReadOnlySpan<byte> overload: it retains the buffer for the
        // document's lifetime, which a stack-only Span can't guarantee) - and, like the string
        // overload it replaces, natively enforces "exactly one JSON value, optionally surrounded
        // by whitespace", throwing JsonException otherwise (e.g. `{"a":1} garbage`). TryParseValue
        // was considered instead but offers no advantage here: it still throws for malformed JSON
        // (its "Try" only covers incomplete multi-segment data, not invalid syntax) while
        // additionally requiring this trailing-content check to be hand-rolled, so Parse is both
        // simpler and no less allocation-free.
        try
        {
            using var _ = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
