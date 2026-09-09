using lazynats.Core;
using lazynats.LiveFeed;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace lazynats.Subscriptions;

internal sealed record SubscriptionInfo(Guid Id, string Pattern);

// Add/Remove are only ever called from the UI thread (button/key handlers), so the
// dictionary needs no locking - the background RunAsync tasks never touch it.
internal sealed class SubscriptionRegistry
{
    private readonly NatsConnection _connection;
    private readonly IObserver<FeedEnvelope> _sink;
    private readonly Dictionary<Guid, (string Pattern, NatsFilter Filter, CancellationTokenSource Cts)> _subscriptions = new();

    public event Action? Changed;

    public SubscriptionRegistry(NatsConnection connection, IObserver<FeedEnvelope> sink)
    {
        _connection = connection;
        _sink = sink;
    }

    public IReadOnlyList<SubscriptionInfo> Active =>
        _subscriptions.Select(kv => new SubscriptionInfo(kv.Key, kv.Value.Pattern)).ToList();

    public Guid Add(string pattern)
    {
        var id = Guid.NewGuid();
        var filter = FilterExpression.TryCompile(pattern)
            ?? throw new ArgumentException($"Pattern '{pattern}' does not compile.", nameof(pattern));
        var cts = new CancellationTokenSource();
        _subscriptions[id] = (pattern, filter, cts);
        _ = RunAsync(id, filter, cts.Token);
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
        catch (OperationCanceledException)
        {
            // Expected when Remove() cancels this subscription's token.
        }
        catch (Exception ex)
        {
            if (_subscriptions.Remove(id, out var entry)) 
                entry.Cts.Dispose();
            Changed?.Invoke();

            // Non-View, non-DI-constructed class reaching UI services - see CLAUDE.md's DI
            // convention (Services.Root.GetRequiredService<T>(), not a static/ambient accessor).
            var app = Services.Root.GetRequiredService<IApplication>();
            app.Invoke(() => MessageBox.ErrorQuery(app, " Subscription Failed ", ex.Message.Pad(), "_Ok"));
        }
    }
}
