using System.Text.Json;

namespace lazynats.Templates;

// Client-side Payload validation, keyed by the currently selected PayloadType - mirrors
// nats-publish's Send-validation shape (see design.md). Json/Base64 both require the payload text
// to actually parse/decode; Text accepts anything, including empty.
internal static class PayloadValidation
{
    public static bool IsValid(PayloadType payloadType, string payload) =>
        payloadType switch
        {
            PayloadType.Json => IsValidJson(payload),
            PayloadType.Base64 => IsValidBase64(payload),
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
}
