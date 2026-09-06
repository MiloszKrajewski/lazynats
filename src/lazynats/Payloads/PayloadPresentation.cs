using System.Text;
using System.Text.Json;
using lazynats.Core;

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

    // Candidate Hex bytes/row and Base64 line-width bounds - see
    // openspec/changes/adaptive-payload-width/design.md Decisions 3-4.
    private static readonly int[] HexBytesPerRowCandidates = [8, 16, 24, 32, 48, 64];
    private const int MinHexBytesPerRow = 8;
    private const int MaxHexBytesPerRow = 64;
    private const int MinBase64LineWidth = 24;
    private const int MaxBase64LineWidth = 144;

    // Callers are responsible for only requesting a type from AllowedTypes(kind) for this payload's
    // own classification - rendering an out-of-set combination (e.g. Json for non-JSON bytes) is
    // not a supported operation, per the payload-presentation spec. `width` is the payload
    // section's available width (see message-detail-dialog spec); Json is the only presentation
    // that ignores it - its own indentation already breaks most payloads into reasonably-sized
    // lines, whereas Text's wire bytes are frequently one unbroken line (e.g. minified JSON viewed
    // as Text rather than Json) with nothing else to keep it inside the visible width.
    public static string Render(byte[] data, PayloadType type, int width) => type switch
    {
        PayloadType.Json => RenderJson(data),
        PayloadType.Text => RenderText(data, width),
        PayloadType.Base64 => RenderBase64(data, width),
        PayloadType.Hex => RenderHex(data, width),
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

    // Wraps at a fixed character count rather than word boundaries (Terminal.Gui's own
    // TextFormatter.WordWrapText, tried first, produces ragged/misleading output here: wire bytes
    // shown as Text are typically minified JSON or similar - one giant run with no spaces except
    // occasionally inside a string value - so its word-boundary logic ends up hard-splitting that
    // run anyway, but only after first leaving a short, mostly-empty line, and the eventual split
    // point still lands wherever the run happened to be at that character offset, not at any
    // actual word boundary; e.g. a `"name":"NATS .NET Client"` value split as "...\"name\":\"N" /
    // "ATS .NET" (short line) / "Client\",...". A plain, consistent chop matches Hex/Base64's own
    // "fit what you're given" approach and reads as deliberate rather than broken). Existing line
    // breaks are preserved and each resulting line is wrapped independently.
    private static string RenderText(byte[] data, int width)
    {
        var text = Encoding.UTF8.GetString(data);
        var lines = text.Split('\n').SelectMany(line => ChunkFixedWidth(line, width));
        return string.Join('\n', lines);
    }

    // Bytes/row sized to the available width, space-separated - enough to eyeball structure/length
    // without an offset column or ASCII gutter. See adaptive-payload-width design.md Decision 3.
    private static string RenderHex(byte[] data, int width)
    {
        var bytesPerRow = HexBytesPerRow(width);
        var builder = new StringBuilder();
        for (var offset = 0; offset < data.Length; offset += bytesPerRow)
        {
            var end = Math.Min(offset + bytesPerRow, data.Length);
            for (var i = offset; i < end; i++)
            {
                if (i > offset) builder.Append(' ');
                builder.Append(data[i].ToString("X2"));
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    // Largest candidate whose formatted row ("XX" pairs, single-space separated, no trailing
    // space - 3n-1 chars for n bytes) still fits width; falls back to the smallest candidate when
    // even that doesn't fit. Clamp is a safety net around the candidate set's own [8, 64] bounds.
    private static int HexBytesPerRow(int width) =>
        HexBytesPerRowCandidates.Where(n => 3 * n - 1 <= width).DefaultIfEmpty(MinHexBytesPerRow).Max()
            .NotLessThan(MinHexBytesPerRow).NotMoreThan(MaxHexBytesPerRow);

    // Wraps the unbroken base64 string into lines sized to the available width, floored to the
    // nearest multiple of 4 (4 encoded chars = 3 raw bytes, keeping breaks on clean quantum
    // boundaries). See adaptive-payload-width design.md Decision 4.
    private static string RenderBase64(byte[] data, int width)
    {
        var text = Convert.ToBase64String(data);
        var lineWidth = (width / 4 * 4).NotLessThan(MinBase64LineWidth).NotMoreThan(MaxBase64LineWidth);
        return string.Join('\n', ChunkFixedWidth(text, lineWidth));
    }

    // Splits a single line (no embedded '\n') into chunks of at most `width` characters each.
    // Always yields at least one chunk, even for an empty line, so a genuinely blank input line
    // maps to one blank output line rather than disappearing. `width` is clamped to at least 1 -
    // callers pass an already-clamped candidate/floor (Hex/Base64) or the dialog's raw measured
    // width (Text), and a non-positive width would otherwise loop forever.
    private static IEnumerable<string> ChunkFixedWidth(string line, int width)
    {
        var safeWidth = width.NotLessThan(1);
        if (line.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        for (var offset = 0; offset < line.Length; offset += safeWidth)
            yield return line.Substring(offset, Math.Min(safeWidth, line.Length - offset));
    }
}
