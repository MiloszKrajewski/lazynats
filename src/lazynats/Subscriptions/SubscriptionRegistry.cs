using System.Collections.Concurrent;
using lazynats.Core;
using lazynats.LiveFeed;
using NATS.Client.Core;

namespace lazynats.Subscriptions;

internal sealed record SubscriptionInfo(Guid Id, string Pattern): ISubscriptionInfo;

internal sealed class Subscription: ISubscriptionInfo
{
    public required Guid Id { get; init; }
    public required string Pattern { get; init; }
    public required NatsFilter Filter { get; init; }
    public required CancellationTokenSource Cts { get; init; }
}

// Add/Remove (the public API) are only ever called from the UI thread; Remove is the only method
// that mutates _subscriptions. RunAsync (background-task thread) only reads it via TryGetValue and
// raises Failed, relying on a Failed subscriber to call Remove() - it never removes or disposes the
// entry itself. ConcurrentDictionary remains necessary because that background-thread read still
// races the UI-thread Add/Remove calls.
internal sealed class SubscriptionRegistry
{
    private readonly NatsConnection _connection;
    private readonly IObserver<FeedEnvelope> _sink;
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();

    public event Action? Changed;
    public event Action<ISubscriptionInfo, Exception>? Failed;

    public SubscriptionRegistry(NatsConnection connection, IObserver<FeedEnvelope> sink)
    {
        _connection = connection;
        _sink = sink;
    }

    public IReadOnlyList<ISubscriptionInfo> Active => _subscriptions.Values.ToList();

    public Guid Add(string pattern)
    {
        var id = Guid.NewGuid();
        var filter = FilterExpression.TryCompile(pattern)
            ?? throw new ArgumentException($"Pattern '{pattern}' does not compile.", nameof(pattern));
        var cts = new CancellationTokenSource();
        _subscriptions[id] = new Subscription { Id = id, Pattern = pattern, Filter = filter, Cts = cts };
        _ = RunAsync(id, filter, cts.Token);
        Changed?.Invoke();
        return id;
    }

    public void Remove(Guid id)
    {
        if (!_subscriptions.TryRemove(id, out var entry)) return;

        entry.Cts.Cancel();
        entry.Cts.Dispose();
        Changed?.Invoke();
    }

    private async Task RunAsync(Guid id, NatsFilter filter, CancellationToken cancellationToken)
    {
        var nativeFilter = filter.Native;
        var clientFilter = !filter.NativeFilterIsExact ? filter.Client : null;

        try
        {
            var subscription = _connection.SubscribeAsync<byte[]>(nativeFilter, cancellationToken: cancellationToken);
            await foreach (var message in subscription)
            {
                var isMatch = clientFilter is null || clientFilter.IsMatch(message.Subject);
                if (!isMatch) continue;

                var envelope = new FeedEnvelope(DateTimeOffset.UtcNow, id, message);
                _sink.OnNext(envelope);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when Remove() cancels this subscription's own token.
        }
        catch (Exception ex)
        {
            if (!_subscriptions.TryGetValue(id, out var entry)) return;

            Failed?.Invoke(entry, ex);
        }
    }
}
