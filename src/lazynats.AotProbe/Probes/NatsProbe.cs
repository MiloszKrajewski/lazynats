using NATS.Client.Core;

namespace lazynats.AotProbe.Probes;

internal static class NatsProbe
{
    public static async Task Run()
    {
        await using var connection = new NatsConnection(new NatsOpts { Url = "nats://localhost:14442" });

        try {
            await connection.ConnectAsync();
        } catch (Exception ex) {
            throw new InvalidOperationException(
                $"NATS connection failed - test-aot.ps1 should have started a probe-only nats-server on port 14442 ({ex.GetType().Name}: {ex.Message})", ex);
        }

        const string subject = "aotprobe.ping";
        var payload = "hello"u8.ToArray();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscriberReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptionTask = Task.Run(async () => {
            var enumerator = connection.SubscribeAsync<byte[]>(subject, cancellationToken: cts.Token).GetAsyncEnumerator(cts.Token);
            subscriberReady.TrySetResult();
            if (await enumerator.MoveNextAsync())
                received.TrySetResult(enumerator.Current.Data ?? []);
        }, cts.Token);

        await subscriberReady.Task.WaitAsync(cts.Token);
        await Task.Delay(200, cts.Token);
        await connection.PublishAsync(subject, payload, cancellationToken: cts.Token);

        var result = await received.Task.WaitAsync(cts.Token);
        if (!result.SequenceEqual(payload))
            throw new InvalidOperationException("Received payload did not match published payload.");
    }
}
