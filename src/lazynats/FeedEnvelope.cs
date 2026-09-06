using NATS.Client.Core;

namespace lazynats;

internal sealed record FeedEnvelope(DateTimeOffset ReceivedAt, Guid SubscriptionId, NatsMsg<byte[]> Message);
