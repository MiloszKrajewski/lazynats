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

    // Buffer sized to payload.Length is always large enough - base64 decoding never produces more
    // bytes than the encoded text's own length.
    private static bool IsValidBase64(string payload) =>
        Convert.TryFromBase64String(payload, new byte[payload.Length], out _);

    private static bool IsValidHex(string payload)
    {
        try
        {
            Convert.FromHexString(payload);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
