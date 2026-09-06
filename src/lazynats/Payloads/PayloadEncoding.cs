using System.Text;

namespace lazynats.Payloads;

// Converts an already-validated payload's text to the raw bytes it is sent/stored as - see
// design.md's "Shared payload-types capability" decision.
internal static class PayloadEncoding
{
    public static byte[] ToBytes(PayloadType type, string payload) => type switch
    {
        PayloadType.Base64 => Convert.FromBase64String(payload),
        PayloadType.Hex => Convert.FromHexString(payload),
        _ => Encoding.UTF8.GetBytes(payload), // Json, Text: sent as their own UTF-8 text
    };
}
