using System.Text.Json.Nodes;
using lazynats.Payloads;

namespace lazynats.Templates;

// Converts between the flat UI/edit string (what TemplateDialog/TemplateDetails/TemplatesTab's
// callers see) and the JSON-document Payload node actually persisted, per PayloadType - see
// design.md's "TemplatePayloadCodec" decision. Templates-specific (unlike PayloadValidation/
// PayloadEncoding): the document shape is a Templates concept, not a shared one.
internal static class TemplatePayloadCodec
{
    // For Json, the payload text is already validated JSON at this point, so it parses cleanly
    // into a native JsonNode; for Text, the node is just a plain JSON string wrapping the text
    // unchanged. For Hex/Base64, the caller's payload text may carry whitespace formatting (per
    // payload-types' whitespace-tolerant validation) - round-trip it through PayloadEncoding.ToBytes
    // and back to a canonical, whitespace-free encoded string, so the stored form is normalized
    // rather than whatever whitespace the user typed or pasted (see design.md's "Templates store
    // the normalized form" decision).
    public static JsonNode ToNode(PayloadType type, string payload) => type switch
    {
        PayloadType.Json => JsonNode.Parse(payload)!,
        PayloadType.Hex => JsonValue.Create(Convert.ToHexString(PayloadEncoding.ToBytes(type, payload))),
        PayloadType.Base64 => JsonValue.Create(Convert.ToBase64String(PayloadEncoding.ToBytes(type, payload))),
        _ => JsonValue.Create(payload),
    };

    // For Text/Base64/Hex, the node is always a plain JSON string - unwrap it directly. For Json,
    // a JsonValue wrapping a string means the *old* string-encoded storage shape (or a
    // hand-written import file using it) - return that string as-is, since it's already the raw
    // JSON text. Any other node shape (JsonObject/JsonArray/non-string JsonValue) is the *new*
    // native-JSON shape - round-trip it back to text via ToJsonString.
    public static string ToText(PayloadType type, JsonNode? node)
    {
        if (type != PayloadType.Json) return node?.GetValue<string>() ?? string.Empty;

        return node is JsonValue value && value.TryGetValue(out string? text)
            ? text
            : node?.ToJsonString() ?? string.Empty;
    }
}
