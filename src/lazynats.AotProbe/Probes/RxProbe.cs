using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace lazynats.AotProbe.Probes;

internal static class RxProbe
{
    public static async Task Run()
    {
        var subject = new Subject<string>();
        string? received = null;
        using var subscription = subject.Subscribe(value => received = value);

        subject.OnNext("hello");

        if (received != "hello")
            throw new InvalidOperationException($"Expected subscriber to receive 'hello', got '{received}'.");

        await RunSynchronizeAndBuffer();
    }

    // Exercises the two APIs the live-feed pipeline (SubscriptionRegistry/LiveUpdatesView) added
    // on top of the basic Subject/Subscribe pair above: Subject.Synchronize (serializes
    // concurrent producer OnNext calls) and Buffer(TimeSpan) (time-windowed batching).
    private static async Task RunSynchronizeAndBuffer()
    {
        var synchronized = Subject.Synchronize(new Subject<int>());

        var received = new List<int>();
        using var subscription = synchronized
            .Buffer(TimeSpan.FromMilliseconds(25))
            .Where(batch => batch.Count > 0)
            .Subscribe(received.AddRange);

        var producers = Enumerable.Range(0, 4)
            .Select(offset => Task.Run(() => {
                for (var i = 0; i < 25; i++) synchronized.OnNext(offset * 100 + i);
            }))
            .ToArray();
        await Task.WhenAll(producers);

        // Comfortably longer than the 25ms buffer window, so every batch - however many the
        // producers' timing happened to split across - has had a chance to flush.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        if (received.Count != 100)
            throw new InvalidOperationException($"Expected 100 values buffered across all batches, got {received.Count}.");
    }
}
