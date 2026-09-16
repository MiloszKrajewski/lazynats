using System.Text.Json;

namespace lazynats.Payloads;

// Client-side Payload validation, keyed by the currently selected PayloadType - shared by Templates
// and Publish (see design.md). Json/Base64/Hex all require the payload text to actually
// parse/decode; Text accepts anything, including empty.
internal static class PayloadValidation
{
    public static bool IsValid(PayloadType payloadType, string payload) =>
        payloadType switch
        {
            PayloadType.Json => IsValidJson(payload),
            PayloadType.Base64 => IsValidBase64(payload),
            PayloadType.Hex => IsValidHex(payload),
            _ => true,
        };

    private static bool IsValidJson(string payload)
    {
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

    // Whitespace-tolerant: see PayloadBinaryText for the boundary-checked scan/decode this shares
    // with PayloadEncoding.ToBytes.
    private static bool IsValidBase64(string payload) => PayloadBinaryText.TryDecodeBase64(payload, out _);

    private static bool IsValidHex(string payload) => PayloadBinaryText.TryDecodeHex(payload, out _);
}
