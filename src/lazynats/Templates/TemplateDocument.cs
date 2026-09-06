using System.Text.Json.Serialization;

namespace lazynats.Templates;

// The JSON shape actually persisted to the lazynats-templates KV bucket, one document per entry -
// see design.md's "Storage shape" decision. Name is deliberately absent: it's already the KV key,
// so repeating it inside the document would be redundant. Headers is a concrete Dictionary<,>
// (rather than Template's IReadOnlyDictionary<,>) since System.Text.Json's source generator needs
// a concrete collection type to generate (de)serialization code for.
internal sealed record TemplateDocument(
    string Subject,
    Dictionary<string, string> Headers,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PayloadType>))]
    PayloadType PayloadType,
    string Payload);

// Source-generated (PublishAot-safe, no reflection) serialization context for TemplateDocument -
// the first JSON the app layer itself serializes/deserializes, rather than only inside the NATS
// client libraries. CamelCase property names match design.md's example document
// ({"subject": ..., "headers": ..., "payloadType": "Json", "payload": ...}); PayloadType's own
// JsonStringEnumConverter above is unaffected by this naming policy, so it still serializes as its
// literal C# enum name (Json/Text/Base64), not camelCased.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TemplateDocument))]
internal partial class TemplateJsonContext: JsonSerializerContext;
