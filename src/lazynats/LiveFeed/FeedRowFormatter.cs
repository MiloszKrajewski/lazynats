using lazynats.Payloads;

namespace lazynats.LiveFeed;

internal static class FeedRowFormatter
{
    private const int MaxPayloadBodyLength = 1024;

    public static string Format(FeedEnvelope envelope)
    {
        var message = envelope.Message;
        var headerText = message.Headers is { Count: > 0 } headers
            ? string.Join(' ', headers.Select(kv => $"{kv.Key}={kv.Value}"))
            : string.Empty;
        var payloadText = RenderPayload(envelope);

        return $"{envelope.ReceivedAt:HH:mm:ss.fff}  {message.Subject}  {headerText}  {payloadText}";
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
