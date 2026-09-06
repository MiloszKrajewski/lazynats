using NATS.Client.Core;

namespace lazynats;

// Window size is a starting guess pending real traffic (see design.md). Only ever
// called from the single FeedReaderLoop that owns it, so no locking is needed.
internal sealed class MessageDeduplicator(TimeSpan window)
{
    private readonly Dictionary<int, DateTimeOffset> _lastSeen = new();

    public bool IsDuplicate(FeedEnvelope envelope)
    {
        var key = ComputeKey(envelope.Message);
        var now = envelope.ReceivedAt;
        var isDuplicate = _lastSeen.TryGetValue(key, out var lastSeenAt) && now - lastSeenAt <= window;

        _lastSeen[key] = now;
        Prune(now);
        return isDuplicate;
    }

    private void Prune(DateTimeOffset now)
    {
        List<int>? expired = null;
        foreach (var (key, seenAt) in _lastSeen)
            if (now - seenAt > window)
                (expired ??= []).Add(key);

        if (expired is null) return;
        foreach (var key in expired) _lastSeen.Remove(key);
    }

    // Subject + headers + payload only. ReceivedAt and SubscriptionId are deliberately
    // excluded - see design.md's "Dedup" decision for why including either would defeat
    // the whole point (one must differ between duplicates, the other never matches core
    // NATS's actual delivery semantics).
    private static int ComputeKey(NatsMsg<byte[]> message)
    {
        var hash = new HashCode();
        hash.Add(message.Subject);

        if (message.Headers is { Count: > 0 } headers)
            foreach (var (headerKey, headerValue) in headers) {
                hash.Add(headerKey);
                hash.Add(headerValue.ToString());
            }

        if (message.Data is { } data)
            foreach (var b in data)
                hash.Add(b);

        return hash.ToHashCode();
    }
}
