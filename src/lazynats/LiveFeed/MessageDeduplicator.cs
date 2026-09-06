using System.IO.Hashing;
using System.Runtime.InteropServices;
using NATS.Client.Core;

namespace lazynats.LiveFeed;

// Window size is a starting guess pending real traffic (see design.md). Only ever called from
// the Where() predicate of the live feed's Rx chain, which runs against a Subject.Synchronize()-
// wrapped subject - notifications are serialized, so no locking is needed here.
internal sealed class MessageDeduplicator(TimeSpan window)
{
    private readonly Dictionary<ulong, (DateTimeOffset Timestamp, Guid SubscriptionId)> _lastSeen = new();

    // A message is a duplicate only if the *same* key was already seen, within the window, from
    // a *different* subscription - see dedup-subscription-freeze/design.md. The stored canonical
    // (Timestamp, SubscriptionId) is updated only when the current envelope is NOT a duplicate;
    // duplicate hits leave it frozen so a later genuine repeat is still judged against the
    // original canonical subscription rather than whichever subscription's echo arrived last.
    public bool IsDuplicate(FeedEnvelope envelope)
    {
        var key = ComputeKey(envelope.Message);
        var now = envelope.ReceivedAt;
        var isDuplicate = _lastSeen.TryGetValue(key, out var canonical)
                        && now - canonical.Timestamp <= window
                        && envelope.SubscriptionId != canonical.SubscriptionId;

        if (!isDuplicate)
            _lastSeen[key] = (now, envelope.SubscriptionId);

        Prune(now);
        return isDuplicate;
    }

    private void Prune(DateTimeOffset now)
    {
        List<ulong>? expired = null;
        foreach (var (key, canonical) in _lastSeen)
            if (now - canonical.Timestamp > window)
                (expired ??= []).Add(key);

        if (expired is null) return;

        foreach (var key in expired) _lastSeen.Remove(key);
    }

    // Subject + headers + payload only. ReceivedAt is excluded - it never matches core NATS's
    // actual delivery semantics, since duplicate deliveries are two distinct receive events with
    // two different receipt times. SubscriptionId is tracked separately, as comparison state in
    // IsDuplicate above, rather than folded into this hash - see
    // dedup-subscription-freeze/design.md. Strings are hashed via their raw UTF-16 bytes (not
    // UTF-8-encoded) since the key only needs internal consistency, not real text.
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
