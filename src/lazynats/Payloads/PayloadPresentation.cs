using System.Text;
using System.Text.Json;

namespace lazynats.Payloads;

// Display-side counterpart to PayloadValidation/PayloadEncoding: those validate/encode
// user-typed text for sending, this renders already-received bytes for display. Reuses the
// PayloadType enum purely as a set of display labels - no PayloadValidation/PayloadEncoding logic
// is invoked here. See openspec/changes/add-payload-presentation-selector/design.md Decisions 1-2.
internal static class PayloadPresentation
{
    // Hex/Base64 are always valid (any byte sequence encodes as either); Text requires the probe's
    // well-formed-UTF-8 text (Json or Utf8Text); Json requires the bytes to actually parse as JSON.
    public static PayloadType[] AllowedTypes(PayloadContentKind kind) => kind switch
    {
        PayloadContentKind.Json => [PayloadType.Json, PayloadType.Text, PayloadType.Hex, PayloadType.Base64],
        PayloadContentKind.Utf8Text => [PayloadType.Text, PayloadType.Hex, PayloadType.Base64],
        _ => [PayloadType.Hex, PayloadType.Base64],
    };

    public static PayloadType DefaultType(PayloadContentKind kind) => kind switch
    {
        PayloadContentKind.Json => PayloadType.Json,
        PayloadContentKind.Utf8Text => PayloadType.Text,
        _ => PayloadType.Hex,
    };

    // Callers are responsible for only requesting a type from AllowedTypes(kind) for this payload's
    // own classification - rendering an out-of-set combination (e.g. Json for non-JSON bytes) is
    // not a supported operation, per the payload-presentation spec.
    public static string Render(byte[] data, PayloadType type) => type switch
    {
        PayloadType.Json => RenderJson(data),
        PayloadType.Text => Encoding.UTF8.GetString(data),
        PayloadType.Base64 => Convert.ToBase64String(data),
        PayloadType.Hex => RenderHex(data),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    // Re-serializes with indentation rather than displaying the decoded text as-is, so a minified
    // wire payload (no insignificant whitespace) still reads as structured JSON. JsonElement.WriteTo,
    // not JsonSerializer.Serialize(document.RootElement, ...) - the latter is reflection-based
    // (RequiresUnreferencedCode/RequiresDynamicCode) and unsafe under PublishAot trimming, per
    // CLAUDE.md; WriteTo needs neither.
    private static string RenderJson(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        using var document = JsonDocument.Parse(text);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            document.RootElement.WriteTo(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // 16 bytes per row, space-separated - enough to eyeball structure/length without an offset
    // column or ASCII gutter.
    private static string RenderHex(byte[] data)
    {
        var builder = new StringBuilder();
        for (var offset = 0; offset < data.Length; offset += 16)
        {
            var end = Math.Min(offset + 16, data.Length);
            for (var i = offset; i < end; i++)
            {
                if (i > offset) builder.Append(' ');
                builder.Append(data[i].ToString("X2"));
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
