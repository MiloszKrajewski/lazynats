using lazynats.Payloads;
using NATS.Client.Core;

namespace lazynats.LiveFeed;

internal sealed record FeedEnvelope(
    DateTimeOffset ReceivedAt,
    Guid SubscriptionId,
    NatsMsg<byte[]> Message
)
{
    // Lazily populated by FeedRowFormatter on first render, not on receipt - keeps
    // classification/rendering cost scoped to what's actually scrolled into view rather than total
    // feed throughput. UI-thread-only, same as the rest of the render path - see FeedRowFormatter.
    public PayloadContentKind? CachedContentKind { get; set; }
    public string? CachedPayloadBody { get; set; }
}
