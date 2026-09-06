// Throwaway load generator for stress-testing lazynats's live feed. NOT part of lazynats.sln -
// see src/lazynats.AotProbe for the same "standalone project under src/" precedent. Blasts
// messages at a NATS server as fast as possible (or at a throttled rate) so you can watch how
// the live feed behaves - and where it falls over - under sustained high throughput.
//
// Usage:
//   dotnet run --project src/lazynats.FeedLoadGen -- [options]
//
// Options (all optional):
//   --url <url>          NATS server URL              (default: nats://localhost:4222)
//   --subject <subject>  Subject to publish on; "{seq}" is replaced with the zero-padded sequence
//                        number, letting each message carry a unique, feed-visible identity
//                        (default: loadgen.feed)
//   --rate <n>            Messages/sec, 0 = unthrottled (default: 0)
//   --size <bytes>        Payload size in bytes         (default: 64)
//   --duration <seconds>  0 = run until Ctrl+C          (default: 0)
//   --parallel <n>        Concurrent publisher tasks    (default: 1)
//   --count <n>           Exact number to send, then exit; overrides --duration (default: 0 = off)
//
// Prints a running total + instantaneous rate once a second so you can correlate what you see in
// lazynats against an actual send rate.

using System.Text;
using NATS.Client.Core;

var options = LoadGenOptions.Parse(args);

Console.WriteLine($"""
    lazynats feed load generator
      url:      {options.Url}
      subject:  {options.Subject}
      rate:     {(options.Rate == 0 ? "unthrottled" : $"{options.Rate}/sec")}
      size:     {options.Size} bytes
      duration: {(options.Duration == TimeSpan.Zero ? "until Ctrl+C" : options.Duration.ToString())}
      parallel: {options.Parallel}
    """);

await using var connection = new NatsConnection(new NatsOpts { Url = options.Url });
await connection.ConnectAsync();

using var cts = new CancellationTokenSource();
if (options.Count == 0 && options.Duration > TimeSpan.Zero) cts.CancelAfter(options.Duration);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var sent = 0L;
var statsTask = ReportStats(() => Interlocked.Read(ref sent), cts.Token);

void OnSent()
{
    if (Interlocked.Increment(ref sent) >= options.Count && options.Count > 0) cts.Cancel();
}

var workers = Enumerable.Range(0, options.Parallel)
    .Select(_ => Publish(connection, options, OnSent, cts.Token));

try {
    await Task.WhenAll(workers);
} catch (OperationCanceledException) {
    // Expected on Ctrl+C or --duration expiry.
}

await cts.CancelAsync();
await statsTask;

Console.WriteLine($"Done. Sent {sent} messages total.");
return;

static async Task Publish(NatsConnection connection, LoadGenOptions options, Action onSent, CancellationToken ct)
{
    var delay = options.Rate > 0 ? TimeSpan.FromSeconds(1.0 / options.Rate * options.Parallel) : TimeSpan.Zero;
    var seq = 0L;

    while (!ct.IsCancellationRequested) {
        var payload = BuildPayload(seq, options.Size);
        var subject = options.Subject.Replace("{seq}", $"{seq:D8}");
        seq++;

        try {
            await connection.PublishAsync(subject, payload, cancellationToken: ct);
        } catch (OperationCanceledException) {
            break;
        }

        onSent();

        if (delay > TimeSpan.Zero) {
            try {
                await Task.Delay(delay, ct);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}

static byte[] BuildPayload(long seq, int size)
{
    // Sequence number up front so each message is distinguishable in the live feed (and so
    // ordering/dedup behavior is eyeball-checkable); padded with dots out to `size` bytes.
    var prefix = Encoding.ASCII.GetBytes($"#{seq:D8} ");
    var payload = new byte[Math.Max(size, prefix.Length)];
    Array.Fill(payload, (byte) '.');
    prefix.CopyTo(payload, 0);
    return payload;
}

static async Task ReportStats(Func<long> getSent, CancellationToken ct)
{
    var last = 0L;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var lastElapsed = TimeSpan.Zero;

    try {
        while (!ct.IsCancellationRequested) {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var now = getSent();
            var elapsed = sw.Elapsed;
            var intervalRate = (now - last) / (elapsed - lastElapsed).TotalSeconds;
            Console.WriteLine($"[{elapsed:mm\\:ss}] sent={now} rate={intervalRate:F0}/s");
            last = now;
            lastElapsed = elapsed;
        }
    } catch (OperationCanceledException) {
        // Expected on shutdown.
    }
}

internal sealed record LoadGenOptions(
    string Url, string Subject, int Rate, int Size, TimeSpan Duration, int Parallel, int Count)
{
    public static LoadGenOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
            map[args[i].TrimStart('-')] = args[i + 1];

        return new LoadGenOptions(
            Url: map.GetValueOrDefault("url", "nats://localhost:4222"),
            Subject: map.GetValueOrDefault("subject", "loadgen.feed"),
            Rate: int.Parse(map.GetValueOrDefault("rate", "0")),
            Size: int.Parse(map.GetValueOrDefault("size", "64")),
            Duration: TimeSpan.FromSeconds(double.Parse(map.GetValueOrDefault("duration", "0"))),
            Parallel: int.Parse(map.GetValueOrDefault("parallel", "1")),
            Count: int.Parse(map.GetValueOrDefault("count", "0")));
    }
}
