using System.Reactive.Subjects;

namespace lazynats.AotProbe.Probes;

internal static class RxProbe
{
    public static Task Run()
    {
        var subject = new Subject<string>();
        string? received = null;
        using var subscription = subject.Subscribe(value => received = value);

        subject.OnNext("hello");

        if (received != "hello")
            throw new InvalidOperationException($"Expected subscriber to receive 'hello', got '{received}'.");

        return Task.CompletedTask;
    }
}
