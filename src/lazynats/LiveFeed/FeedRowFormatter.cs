using lazynats.Payloads;
using Terminal.Gui.Drawing;

namespace lazynats.LiveFeed;

// Generic (color, text) piece of a rendered row - named generically, not FeedRowSegment, because
// nothing about the shape is feed-specific; only FeedRowFormatter (assembly) and
// LiveLogDataSource (the FeedEnvelope binding) actually are. `Color: null` means "leave whatever
// attribute the row already has (normal or selected) alone" - see live-feed's "Row Subject/Header
// Text Is Colored" requirements and doc/multi-color-rendering.md idiom 2.
internal readonly record struct RowSegment(Color? Color, string Text);

// An ordered, contiguous sequence of RowSegments making up one row's full (unclipped) text -
// replaces a single Text string plus offset pairs so a renderer can walk it generically
// regardless of how many colored regions a row has.
internal readonly record struct ColoredRow(IReadOnlyList<RowSegment> Segments);

internal static class FeedRowFormatter
{
    private const int MaxPayloadBodyLength = 1024;

    public static ColoredRow Format(FeedEnvelope envelope)
    {
        var message = envelope.Message;
        var timestampText = $"{envelope.ReceivedAt:HH:mm:ss.fff}";
        var headerText = message.Headers is { Count: > 0 } headers
            ? string.Join(' ', headers.Select(kv => $"{kv.Key}={kv.Value}"))
            : string.Empty;
        var (prefix, body) = RenderPayload(envelope);

        var segments = new List<RowSegment>(6)
        {
            new(null, $"{timestampText}  "),
            new(Theme.SubjectColor, message.Subject),
        };
        if (headerText.Length > 0)
        {
            segments.Add(new(null, "  "));
            segments.Add(new(Theme.HeaderColor, headerText));
        }
        segments.Add(new(null, "  "));
        segments.Add(new(Theme.PayloadTypeColor, prefix));
        if (body.Length > 0) segments.Add(new(null, body));

        return new ColoredRow(segments);
    }

    // Classification and body rendering are cached on the envelope, computed at most once - and
    // only on the first render of a row actually scrolled into view, not on message receipt - per
    // live-feed's "Payload Classification and Rendering Are Cached Per Envelope" requirement. The
    // prefix is a cheap literal lookup, recomputed fresh every render - see
    // PayloadPresentation.SingleLinePrefix.
    private static (string Prefix, string Body) RenderPayload(FeedEnvelope envelope)
    {
        var data = envelope.Message.Data ?? [];
        var kind = envelope.CachedContentKind ??= PayloadContentProbe.Classify(data);
        var body = envelope.CachedPayloadBody ??=
            PayloadPresentation.RenderSingleLineBody(data, kind, MaxPayloadBodyLength);
        var prefix = PayloadPresentation.SingleLinePrefix(kind, body.Length == 0);
        return (prefix, body);
    }
}
