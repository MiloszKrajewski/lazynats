using System.Text;

namespace lazynats.Core.Payloads;

// Converts an already-validated payload's text to the raw bytes it is sent/stored as - see
// design.md's "Shared payload-types capability" decision.
internal static class PayloadEncoding
{
    public static byte[] ToBytes(PayloadType type, string payload) => type switch {
        PayloadType.Base64 => Decode(payload, PayloadBinaryText.TryDecodeBase64),
        PayloadType.Hex => Decode(payload, PayloadBinaryText.TryDecodeHex),
        _ => Encoding.UTF8.GetBytes(payload), // Json, Text: sent as their own UTF-8 text
    };

    private delegate bool Decoder(ReadOnlySpan<char> text, out byte[] bytes);

    // Callers only reach here with payload text PayloadValidation.IsValid already accepted, so this
    // shared decoder (see PayloadBinaryText) is expected to succeed - its own boundary/format
    // checks are exactly what validation already ran.
    private static byte[] Decode(string payload, Decoder decode) =>
        decode(payload, out var bytes) ? bytes : throw new FormatException("Invalid encoded payload.");
}
