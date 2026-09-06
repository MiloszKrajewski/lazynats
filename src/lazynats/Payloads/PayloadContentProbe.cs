using System.Text;
using System.Text.Json;

namespace lazynats.Payloads;

// Classifies raw payload bytes for display purposes - the inverse of the PayloadType/
// PayloadValidation/PayloadEncoding trio (which validate/encode already-chosen-type text for
// sending), so kept as its own component rather than folded into those. See design.md's "strict
// UTF-8 decoding + a printable-content check, then a JSON parse attempt" decision for why both a
// plain "looks like printable ASCII" heuristic and "any well-formed UTF-8 is text" were rejected.
internal static class PayloadContentProbe
{
    // encoderShouldEmitUTF8Identifier: false - probing bytes, never producing new bytes, so a BOM
    // preference is irrelevant. throwOnInvalidBytes: true - GetString throws on any malformed or
    // overlong byte sequence instead of silently substituting U+FFFD (what Encoding.UTF8.GetString
    // does), which is what makes the first check below a strict well-formedness check rather than
    // a lossy guess.
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static PayloadContentKind Classify(byte[] payload)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return PayloadContentKind.Binary;
        }

        // Binary data can, by chance, still be well-formed UTF-8 - reject it here if the decoded
        // text carries control characters a genuine text/JSON payload wouldn't, rather than
        // rendering it as "text" full of control-character noise. Tab/newline/carriage-return are
        // the only control characters a legitimate text payload uses, so those alone are exempt.
        foreach (var c in text)
        {
            if (char.IsControl(c) && c is not ('\t' or '\n' or '\r'))
                return PayloadContentKind.Binary;
        }

        return IsJson(text) ? PayloadContentKind.Json : PayloadContentKind.Utf8Text;
    }

    private static bool IsJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
