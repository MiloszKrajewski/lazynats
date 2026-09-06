using lazynats.Payloads;

namespace lazynats.LiveFeed;

// SubjectStart/SubjectLength locate the subject segment within Text, so a renderer can
// highlight it without re-parsing the row - see live-feed's "Row Subject Text Is Colored"
// requirement.
internal readonly record struct FeedRow(string Text, int SubjectStart, int SubjectLength);

internal static class FeedRowFormatter
{
    private const int MaxPayloadBodyLength = 1024;

    public static FeedRow Format(FeedEnvelope envelope)
    {
        var message = envelope.Message;
        var timestampText = $"{envelope.ReceivedAt:HH:mm:ss.fff}";
        var headerText = message.Headers is { Count: > 0 } headers
            ? string.Join(' ', headers.Select(kv => $"{kv.Key}={kv.Value}"))
            : string.Empty;
        var payloadText = RenderPayload(envelope);

        var subjectStart = timestampText.Length + 2;
        var text = $"{timestampText}  {message.Subject}  {headerText}  {payloadText}";
        return new FeedRow(text, subjectStart, message.Subject.Length);
    }

    // Classification and rendering are cached on the envelope, computed at most once - and only on
    // the first render of a row actually scrolled into view, not on message receipt - per
    // live-feed's "Payload Classification and Rendering Are Cached Per Envelope" requirement.
    private static string RenderPayload(FeedEnvelope envelope)
    {
        if (envelope.CachedRowText is { } cached) return cached;

        var data = envelope.Message.Data ?? [];
        var kind = envelope.CachedContentKind ??= PayloadContentProbe.Classify(data);
        var rendered = PayloadPresentation.RenderSingleLine(data, kind, MaxPayloadBodyLength);
        envelope.CachedRowText = rendered;
        return rendered;
    }
}
