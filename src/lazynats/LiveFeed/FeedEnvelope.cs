using NATS.Client.Core;

namespace lazynats.LiveFeed;

internal sealed record FeedEnvelope(
    DateTimeOffset ReceivedAt,
    Guid SubscriptionId,
    NatsMsg<byte[]> Message
);
