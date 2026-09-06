using System.Threading.Channels;
using NATS.Client.Core;

namespace lazynats;

internal sealed record SubscriptionInfo(Guid Id, string Pattern);

// Add/Remove are only ever called from the UI thread (button/key handlers), so the
// dictionary needs no locking - the background RunAsync tasks never touch it.
internal sealed class SubscriptionRegistry
{
    private readonly NatsConnection _connection;
    private readonly ChannelWriter<FeedEnvelope> _writer;
    private readonly Dictionary<Guid, (string Pattern, CancellationTokenSource Cts)> _subscriptions = new();

    public event Action? Changed;

    public SubscriptionRegistry(NatsConnection connection, ChannelWriter<FeedEnvelope> writer)
    {
        _connection = connection;
        _writer = writer;
    }

    public IReadOnlyList<SubscriptionInfo> Active =>
        _subscriptions.Select(kv => new SubscriptionInfo(kv.Key, kv.Value.Pattern)).ToList();

    public Guid Add(string pattern)
    {
        var id = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        _subscriptions[id] = (pattern, cts);
        _ = RunAsync(id, pattern, cts.Token);
        Changed?.Invoke();
        return id;
    }

    public void Remove(Guid id)
    {
        if (!_subscriptions.Remove(id, out var entry)) return;
        entry.Cts.Cancel();
        entry.Cts.Dispose();
        Changed?.Invoke();
    }

    private async Task RunAsync(Guid id, string pattern, CancellationToken cancellationToken)
    {
        try {
            await foreach (var message in _connection.SubscribeAsync<byte[]>(pattern, cancellationToken: cancellationToken)) {
                var envelope = new FeedEnvelope(DateTimeOffset.UtcNow, id, message);
                await _writer.WriteAsync(envelope, cancellationToken);
            }
        } catch (OperationCanceledException) {
            // Expected when Remove() cancels this subscription's token.
        }
    }
}
