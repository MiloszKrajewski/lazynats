---
name: deep-trace-diagnostics
description: "Add System.Diagnostics.Trace probes deep inside application internals when there's no DI seam to inject a logger and no safe place to Console.WriteLine (a GUI/TUI owns the screen, a hot path can't take a new parameter) — then observe them either live via a throwaway cross-process trace watcher, or captured/asserted on inside an xunit test. Use when debugging internals with no other observation point, when asked to trace/instrument/watch what's happening inside running code, or when a test needs to assert on behavior with no public seam to hook. Also use when asked to explain or reuse this trace-probe/trace-watcher technique itself. Not project-specific — self-contained, works in any .NET codebase."
---

# Deep trace diagnostics

A narrow escape hatch, not a general logging style: asserting on trace output couples a test to
an implementation detail. Reach for this only when there's genuinely no other seam — a normal
`ILogger`/testable return value beats this whenever one is available.

Built on `System.Diagnostics.Trace` specifically because it's BCL-only — zero new package,
no AOT/trim risk, and a probe is one line with no static field and no signature change.

---

## Step 0 — confirm it's warranted

No DI seam, no safe output channel, and the thing you need to see (a branch taken, a loop
count, a cache hit/miss, a background task's internal state) isn't reachable any other way.
If it is, use that instead.

## Step 1 — add the probe (emission side, always this trivial)

```csharp
Trace.WriteLine("cache miss for " + key, "App.Module.Member");   // ad hoc, no severity
Trace.TraceWarning("retry #3 — connection reset");                 // real TraceEventType
```

- `category` is a hierarchical string, your choice, no registry (`"App.Module.Member"`).
- Prefer `TraceWarning`/`TraceError`/`TraceInformation` over `WriteLine` when severity is
  actually meaningful — those carry a real `TraceEventType` a listener can filter/assert on.
- If you intend to *assert* on a specific record later, key on `category` (+ `eventType`/`id`),
  not on `message` substrings — message wording is the part most likely to get refactored later.
- Delete the probe once the bug is understood, unless there's a deliberate reason to keep it.

## Step 2 — pick a consumer

| Need | Consumer |
|---|---|
| Watch a running process live (yourself, or so an agent can read it) | cross-process watcher — Step 3a |
| A test needs to assert on internal behavior it can't otherwise observe | xunit capture — Step 3b |

Both attach to the same static `Trace.Listeners` — nothing about Step 1 changes based on which
consumer ends up listening.

---

## Step 3a — live, cross-process (a mini watcher for a running app)

A physical channel is required here — the free zero-setup path (`OutputDebugString`
on Windows / `syslog` on Linux) exists but flattens `category` into the message text before it
ever leaves the process, is genuinely system-wide noise (every process on the box), and syslog
isn't guaranteed to exist in minimal containers. Own the channel instead.

**Use TCP loopback, not a named pipe.** `NamedPipeServerStream` is a real Win32 pipe on Windows
(any language can dial it), but on Linux it's a Unix domain socket at a path that's .NET's own
internal implementation detail, not a documented cross-language contract. A TCP loopback socket
is identical on every OS and speakable from any language with zero platform-specific code —
which matters here since the watcher is deliberately meant to be a quick, disposable,
any-language script, not a permanent component.

**Wire format**: one JSON object per line on the socket (`\n`-delimited — safe because JSON
string-escapes embedded newlines, so splitting on `\n` between records is unambiguous):

```json
{"ts":"2026-09-20T12:34:56.789Z","cat":"App.Feed.Dedup","lvl":"Warning","id":null,"msg":"dropped dup seq=42"}
```

**Listener to add inside the target app** — one line at startup
(`Trace.Listeners.Add(new TcpTraceListener())`), nothing else changes at any call site:

```csharp
public sealed class TcpTraceListener : TraceListener
{
    private readonly Channel<string> _outbox =
        Channel.CreateBounded<string>(new BoundedChannelOptions(1024) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly List<NetworkStream> _clients = new();

    public TcpTraceListener(int port = 47441)
    {
        // IsThreadSafe stays false (the default) — TraceInternal already lock(listener)s around
        // every call for a non-thread-safe listener, so Record() below needs no lock of its own.
        _ = AcceptLoopAsync(port);
        _ = FlushLoopAsync();
    }

    public override void Write(string? message) => Record(null, message, null, null);
    public override void Write(string? message, string? category) => Record(category, message, null, null);
    public override void WriteLine(string? message) => Record(null, message, null, null);
    public override void WriteLine(string? message, string? category) => Record(category, message, null, null);

    public override void TraceEvent(TraceEventCache? cache, string source, TraceEventType eventType, int id, string? message)
        => Record(source, message, eventType, id);

    // Gap in the base class: this overload does NOT delegate to the 5-arg TraceEvent above —
    // its default body calls the private WriteHeader/WriteLine/WriteFooter chain directly, which
    // would leak "source EventType: id :" header fragments into the capture as spurious records.
    // Override it explicitly so every emission path funnels through Record().
    public override void TraceEvent(TraceEventCache? cache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
        => Record(source, args != null ? string.Format(format ?? "", args) : format, eventType, id);

    private void Record(string? category, string? message, TraceEventType? eventType, int? id)
    {
        var json = JsonSerializer.Serialize(new
        {
            ts = DateTime.UtcNow, cat = category, lvl = eventType?.ToString(), id, msg = message ?? ""
        });
        _outbox.Writer.TryWrite(json);   // never blocks the traced call site; drops under backpressure
    }

    private async Task AcceptLoopAsync(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            lock (_clients) _clients.Add(client.GetStream());
        }
    }

    private async Task FlushLoopAsync()
    {
        await foreach (var line in _outbox.Reader.ReadAllAsync())
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            lock (_clients)
                foreach (var s in _clients) try { s.Write(bytes); } catch { /* client gone, ignore */ }
        }
    }
}
```

The send path is deliberately non-blocking (`TryWrite` into a bounded channel, background task
does the real socket I/O) — `Record` runs on the caller's thread inside the BCL's
`lock(listener)`, so slow network I/O directly in `Record` would stall every `Trace` call
anywhere in the app while a client is briefly slow to read.

**Watcher** — genuinely any language; the fastest same-ecosystem option on this machine is
`dotnet-script` (already installed globally — `dotnet script watch.csx`, no project scaffolding):

```csharp
// watch.csx
using System.Net.Sockets;
using System.Text.Json;

using var client = new TcpClient();
client.Connect("127.0.0.1", 47441);
using var reader = new StreamReader(client.GetStream());
string? line;
while ((line = reader.ReadLine()) != null)
{
    var rec = JsonSerializer.Deserialize<JsonElement>(line);
    Console.WriteLine($"[{rec.GetProperty("cat")}] {rec.GetProperty("msg")}");
}
```

A Python/Node/Go equivalent is the same shape: connect a TCP socket to `127.0.0.1:47441`,
read newline-delimited JSON, print. Add category/level filtering as CLI args once there's
enough noise to warrant it.

## Step 3b — in-process, xunit capture

No channel needed — `Trace.Listeners.Add(listener)` inside the test process makes every
subsequent `Trace` call a direct, synchronous method call.

Before adding a new one, check whether the test project already has this helper
(`rg "class TraceProbe"`) — reuse it if so.

```csharp
public sealed record TraceRecord(DateTime Timestamp, string? Category, string Message, TraceEventType? EventType, int? Id);

public sealed class TraceProbe : IDisposable
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly Guid _previousId;
    private readonly Listener _listener;
    private readonly List<TraceRecord> _records = new();

    public TraceProbe()
    {
        _previousId = Trace.CorrelationManager.ActivityId;
        Trace.CorrelationManager.ActivityId = _id;
        _listener = new Listener(_id, r => { lock (_records) _records.Add(r); });
        Trace.Listeners.Add(_listener);
    }

    public IReadOnlyList<TraceRecord> Records { get { lock (_records) return _records.ToArray(); } }

    public async Task<TraceRecord> WaitForAsync(Func<TraceRecord, bool> predicate, TimeSpan timeout)
    {
        // Simple polling is fine here — this is a debug tool, not a hot path.
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            lock (_records)
            {
                var match = _records.FirstOrDefault(predicate);
                if (match is not null) return match;
            }
            await Task.Delay(15, cts.Token).ContinueWith(_ => { }); // swallow cancellation
        }
        throw new TimeoutException($"No trace record matched within {timeout}.");
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_listener);
        Trace.CorrelationManager.ActivityId = _previousId;
    }

    private sealed class Listener(Guid ownerId, Action<TraceRecord> onRecord) : TraceListener
    {
        public override bool IsThreadSafe => false; // BCL serializes calls into this instance for us

        public override void Write(string? m) => Record(null, m, null, null);
        public override void Write(string? m, string? c) => Record(c, m, null, null);
        public override void WriteLine(string? m) => Record(null, m, null, null);
        public override void WriteLine(string? m, string? c) => Record(c, m, null, null);
        public override void TraceEvent(TraceEventCache? ec, string s, TraceEventType t, int id, string? m) => Record(s, m, t, id);
        public override void TraceEvent(TraceEventCache? ec, string s, TraceEventType t, int id, string? f, params object?[]? a)
            => Record(s, a != null ? string.Format(f ?? "", a) : f, t, id);

        private void Record(string? category, string? message, TraceEventType? eventType, int? id)
        {
            if (Trace.CorrelationManager.ActivityId != ownerId) return; // not ours — ignore
            onRecord(new TraceRecord(DateTime.UtcNow, category, message ?? "", eventType, id));
        }
    }
}
```

Usage:

```csharp
using var probe = new TraceProbe();
await sut.DoSomethingAsync();
Assert.Contains(probe.Records, r => r.Category == "App.Cache.Lookup" && r.EventType is null);
```

**Limitation to know about before relying on this**: `Trace.CorrelationManager.ActivityId` is
`AsyncLocal`-backed, so it flows forward through `await`/`Task.Run` chains started *from inside*
the `using` scope — but it does **not** retroactively reach an already-running background
loop that started before the probe existed (e.g. this repo's `SubscriptionRegistry`, which owns
one long-lived background task per subscription — see its architecture note in `CLAUDE.md`).
For tracing into a pre-existing background loop, either filter by `category` alone and accept
some cross-talk risk, or force the relevant tests into a non-parallel xunit collection instead
of relying on correlation.

## Step 4 — cleanup

Ad hoc probes from Step 1 are meant to be temporary — remove them once the bug is understood.
The cross-process listener registration (Step 3a) should also come back out of `Program.cs`
once the session is done, unless there's a deliberate reason to leave it running permanently
behind an env var or debug-build check.
