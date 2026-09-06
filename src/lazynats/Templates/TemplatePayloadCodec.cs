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
    // into a native JsonNode; for Text/Base64/Hex, the node is just a plain JSON string wrapping
    // the text unchanged - the same shape these three types have always used.
    public static JsonNode ToNode(PayloadType type, string payload) =>
        type == PayloadType.Json ? JsonNode.Parse(payload)! : JsonValue.Create(payload);

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
