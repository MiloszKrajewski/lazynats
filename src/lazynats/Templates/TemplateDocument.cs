using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using lazynats.Payloads;

namespace lazynats.Templates;

// The JSON shape actually persisted to the lazynats-templates KV bucket, one document per entry -
// see design.md's "Storage shape" decision. Name is deliberately absent: it's already the KV key,
// so repeating it inside the document would be redundant. Headers is a concrete Dictionary<,>
// (rather than Template's IReadOnlyDictionary<,>) since System.Text.Json's source generator needs
// a concrete collection type to generate (de)serialization code for. Payload is a JsonNode? (not a
// plain string) so a Json-typed template's payload is stored as a native JSON value rather than a
// JSON-encoded string - see design.md's "Native JSON payload storage" decision; TemplatePayloadCodec
// is the only place that converts to/from the flat UI/edit string.
internal sealed record TemplateDocument(
    string Subject,
    Dictionary<string, string> Headers,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PayloadType>))]
    PayloadType PayloadType,
    JsonNode? Payload);

// Source-generated (PublishAot-safe, no reflection) serialization context for TemplateDocument -
// the first JSON the app layer itself serializes/deserializes, rather than only inside the NATS
// client libraries. CamelCase property names match design.md's example document
// ({"subject": ..., "headers": ..., "payloadType": "Json", "payload": ...}); PayloadType's own
// JsonStringEnumConverter above is unaffected by this naming policy, so it still serializes as its
// literal C# enum name (Json/Text/Base64), not camelCased. JsonNode's own converter (built into
// System.Text.Json, not reflection-based) handles Payload without any extra attribution here.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(TemplateDocument))]
[JsonSerializable(typeof(Dictionary<string, TemplateDocument>))]
internal partial class TemplateJsonContext: JsonSerializerContext;
