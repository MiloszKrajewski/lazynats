using System.Text;
using lazynats.Payloads;

namespace lazynats.Values;

// Content-aware rendering for the Key Detail panel's value peek - classifies via
// PayloadContentProbe the same way MessageDetailDialog/PayloadDetailSection do, but renders a
// Binary value as a fixed 16-bytes-per-row hex dump rather than PayloadPresentation's
// adaptive-width Hex, since PollingDetailsView<TTarget, TInfo>.BuildBody has no width to give it
// and the peek is explicitly clipped, not scrolled - see
// openspec/changes/kv-value-peek-and-view/design.md Decision 1.
internal static class ValuePeek
{
    private const int BytesPerRow = 16;

    public static string Render(byte[] value)
    {
        var kind = PayloadContentProbe.Classify(value);
        return kind switch
        {
            // Width argument is ignored for Json (PayloadPresentation.Render's own contract) -
            // there's no width to give it here anyway.
            PayloadContentKind.Json => PayloadPresentation.Render(value, PayloadType.Json, 0),
            PayloadContentKind.Utf8Text => Encoding.UTF8.GetString(value),
            PayloadContentKind.Binary => RenderHex(value),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // Fixed 16 bytes/row, space-separated - deliberately not PayloadPresentation's adaptive Hex
    // (see this class's own comment above).
    private static string RenderHex(byte[] data)
    {
        var builder = new StringBuilder();
        for (var offset = 0; offset < data.Length; offset += BytesPerRow)
        {
            var end = Math.Min(offset + BytesPerRow, data.Length);
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
