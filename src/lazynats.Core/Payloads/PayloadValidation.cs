using System.Text.Json;

namespace lazynats.Core.Payloads;

// Client-side Payload validation, keyed by the currently selected PayloadType - shared by Templates
// and Publish (see design.md). Json/Base64/Hex all require the payload text to actually
// parse/decode; Text accepts anything, including empty.
internal static class PayloadValidation
{
    public static bool IsValid(PayloadType payloadType, string payload) =>
        payloadType switch {
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

    // Whitespace-tolerant: see PayloadBinaryText for the boundary-checked scan this shares with
    // PayloadEncoding.ToBytes - routed through the allocation-free validate-only path since only
    // success/failure is needed here, not the decoded bytes.
    private static bool IsValidBase64(string payload) => PayloadBinaryText.IsValidBase64(payload);

    private static bool IsValidHex(string payload) => PayloadBinaryText.IsValidHex(payload);
}
