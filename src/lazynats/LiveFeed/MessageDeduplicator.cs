using System.IO.Hashing;
using System.Runtime.InteropServices;
using NATS.Client.Core;

namespace lazynats.LiveFeed;

// Window size is a starting guess pending real traffic (see design.md). Only ever
// called from the single FeedReaderLoop that owns it, so no locking is needed.
internal sealed class MessageDeduplicator(TimeSpan window)
{
    private readonly Dictionary<ulong, DateTimeOffset> _lastSeen = new();

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
        List<ulong>? expired = null;
        foreach (var (key, seenAt) in _lastSeen)
            if (now - seenAt > window)
                (expired ??= []).Add(key);

        if (expired is null) return;

        foreach (var key in expired) _lastSeen.Remove(key);
    }

    // Subject + headers + payload only. ReceivedAt and SubscriptionId are deliberately
    // excluded - see design.md's "Dedup" decision for why including either would defeat
    // the whole point (one must differ between duplicates, the other never matches core
    // NATS's actual delivery semantics). Strings are hashed via their raw UTF-16 bytes
    // (not UTF-8-encoded) since the key only needs internal consistency, not real text.
    private static ulong ComputeKey(NatsMsg<byte[]> message)
    {
        var hasher = new XxHash3();

        AppendString(hasher, message.Subject);

        if (message.Headers is { Count: > 0 } headers)
            AppendHeaders(hasher, headers);

        if (message.Data is { } data)
            hasher.Append(data);

        return hasher.GetCurrentHashAsUInt64();
    }

    private static void AppendHeaders(XxHash3 hasher, NatsHeaders headers)
    {
        foreach (var (headerKey, headerValue) in headers)
        {
            AppendString(hasher, headerKey);
            foreach (var value in headerValue)
                AppendString(hasher, value);
        }
    }

    private static void AppendString(XxHash3 hasher, string? text)
    {
        if (text is not null)
            AppendString(hasher, text.AsSpan());
    }

    private static void AppendString(XxHash3 hasher, ReadOnlySpan<char> span) =>
        hasher.Append(MemoryMarshal.AsBytes(span));
}
