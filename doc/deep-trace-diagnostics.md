# Deep trace diagnostics

A technique for observing and asserting on application internals when there is no seam to
inject a logger, no safe place to `Console.WriteLine` (a GUI/TUI owns the screen), and
restructuring the code to expose the data isn't an option. Built on
`System.Diagnostics.Trace` specifically because it ships in the BCL — zero new package, zero
version to pin, no AOT/trim risk — and can be dropped into a single method body as one line
with no static field, no constructor change, no DI.

This is deliberately a narrow escape hatch, not a general logging or testing style. Asserting
on trace output couples a test to an implementation detail (what a method happens to say, not
what it returns) — use it only where no other seam exists, not as a substitute for a normal
DI'd `ILogger`/testable API where one is available.

Not lazynats-specific: nothing below should end up referencing lazynats types. Treat it as a
small standalone piece that happens to live in this repo first — same posture
`lazynats.AotProbe` already has (a standalone project that could leave), just applied more
strictly (no project reference back into `lazynats.Core` either).

## Emission side — must stay trivial forever

```csharp
Trace.WriteLine("cache miss for " + key, "MyApp.Cache.Lookup");   // ad hoc probe, no severity
Trace.TraceWarning("retry #3 — connection reset");                 // has a real TraceEventType
```

- `Trace.WriteLine(message, category)` for throwaway, drop-anywhere probes. No severity is
  attached — listeners see this on the `Write`/`WriteLine` override family.
- `Trace.TraceInformation` / `TraceWarning` / `TraceError` when a real severity matters — these
  dispatch through `TraceEvent(eventCache, source, eventType, id, message)` instead, so a
  listener can distinguish "this was logged" from "this was an error."
- `category` is a hierarchical string, caller's choice (e.g. `"App.Module.Member"`), no central
  registry needed.
- If a test intends to assert on a specific record, prefer keying on `category` (and
  `eventType`/`id` if provided) rather than matching substrings in `message` — message text is
  the part most likely to get reworded during an unrelated refactor.

Nothing else changes at the call site regardless of which consumer (below) ends up listening.

## What happens with zero setup, verified by decompiling the BCL

`DefaultTraceListener` is registered automatically; with no custom listener added, every
`Trace` call still goes somewhere — it does not throw and it does not silently vanish:

```csharp
// DebugProvider.WriteToDebugger — Windows (decompiled from the installed .NET 10 runtime)
if (Debugger.IsLogging()) Debugger.Log(0, null, message);
else Interop.Kernel32.OutputDebugString(message ?? string.Empty);

// DebugProvider.Unix.cs — Linux (dotnet/runtime source)
if (Debugger.IsLogging()) Debugger.Log(0, null, message);
else Interop.Sys.SysLog(Interop.Sys.SysLogPriority.LOG_DEBUG, "%s", message);
```

- Windows: `OutputDebugString` — observable with Sysinternals DebugView or an attached debugger,
  with zero registration.
- Linux/Unix: `syslog` at `LOG_DEBUG` — observable via `journalctl`/`/var/log/syslog`. Setting
  `DOTNET_DebugWriteToStdErr=1` before launch additionally mirrors output to stderr
  (ASCII-only, best-effort) — a genuinely free way to watch trace output in a terminal on Linux.
- `Debugger.Log`/`Debugger.IsLogging()` is a hook for an *external, attached native debugger* —
  there's no managed-code way to subscribe to it from your own process. Under `dotnet test`
  there's no attached debugger, so this branch never fires; it's not a usable mechanism for an
  in-process xunit capture.

### Why this repo doesn't rely on the free OS channel

The base `TraceListener` flattens category into the message before either OS sink ever sees it
(decompiled from `System.Diagnostics.TraceSource.dll`):

```csharp
public virtual void WriteLine(string? message, string? category)
{
    ...
    WriteLine(category + ": " + (message ?? string.Empty));
}
```

Three concrete reasons to not depend on it as the primary path:

1. **Structure is lost.** `category` and `message` arrive as one flattened string by the time
   they hit `OutputDebugString`/`syslog` — a watcher has to parse them back apart by convention.
2. **Genuinely system-wide, not app-wide.** `OutputDebugString` is shared by every process on
   the Windows box doing debug output; syslog mixes in every daemon on the Linux box. Neither is
   scoped to one app by construction (DBWIN does carry a PID; syslog's ident tagging depends on
   whether something called `openlog()` first, which the runtime doesn't do).
3. **Syslog isn't guaranteed to exist.** Minimal/distroless containers commonly run no syslog
   daemon — relevant here since this repo has a `ReleaseDocker` build target, not a hypothetical.

Kept as a documented fallback for the one case it's strictly better at: observing code you
genuinely cannot add a listener to at all (nothing to modify, nothing to redeploy) — still
zero-crash, zero-setup, just noisier and less structured than owning the channel.

## Architecture: one convention, two independent consumers

```
              business logic (anywhere, untouched otherwise)
                          │
                Trace.WriteLine(...) / Trace.TraceError(...)
                          │
              ┌───────────┴────────────┐
              │   Trace.Listeners       │  (static, process-wide,
              │   (shared, static)      │   BCL-owned collection)
              └─────┬──────────────┬────┘
                    │              │
     ┌──────────────▼───┐   ┌──────▼──────────────────┐
     │ in-process        │   │ cross-process             │
     │ (xunit capture)   │   │ (CLI watcher for agents)  │
     │ — no channel,     │   │ — real physical channel   │
     │   direct calls    │   │   required                │
     └────────────────────┘   └────────────────────────────┘
```

### In-process consumer (xunit)

No physical channel at all — `Trace.Listeners.Add(myListener)` makes every subsequent
`Trace` call a direct, synchronous method call on the same thread, in the same process. There
is nothing to transport.

- **No locking needed in the capture buffer.** `TraceInternal`'s dispatch loop (decompiled)
  wraps each call to a listener reporting `IsThreadSafe = false` (the default, inherited
  unless overridden) in `lock(listener)`:
  ```csharp
  foreach (TraceListener listener in Listeners) {
      if (!listener.IsThreadSafe) { lock (listener) { listener.Flush(); ... } }
      else { listener.Flush(); }
  }
  ```
  Verified on `Flush`/`Fail`; `Write`/`WriteLine`/`TraceEvent` dispatch through the same
  pattern. A plain `List<TraceRecord>.Add(...)` inside the listener is safe as-is — the BCL is
  already serializing concurrent writers for you.
- **Capture `List<TraceRecord>`, not `List<string>`.** A small record — `Category`, `Message`,
  `EventType` (nullable; only the `TraceEvent` family carries one), `Id` (nullable),
  timestamp — keeps `category`/`message` unflattened and lets assertions key on severity or
  category instead of parsing text. The listener needs to override **both** the
  `Write`/`WriteLine` family (catches `Trace.WriteLine`) and the `TraceEvent`/`TraceData` family
  (catches `Trace.TraceError`/`Warning`/`Information`) to normalize every emission shape into
  one record type.
- **Isolation under xunit's default parallel test execution.** `Trace.Listeners` is one shared
  static collection; two concurrently-running tests would otherwise cross-contaminate each
  other's capture. `Trace.CorrelationManager.ActivityId` is `AsyncLocal`-backed (flows through
  `await`/`Task.Run` automatically) and requires **no change at any emission call site** — a
  capture scope sets a fresh id on entry, the listener stamps/filters records by the ambient id
  at write time, and restores the previous id on dispose. Each concurrent test gets an isolated
  view of a genuinely shared static sink.
- **Background/async traced code needs a wait, not just a snapshot.** A lot of "can't otherwise
  observe this" cases are exactly background work (a channel reader, a subscription task, an
  event handler firing later). Expose `WaitForAsync(predicate, timeout)` alongside a synchronous
  snapshot — a capture API that only supports "assert on what's already arrived" quietly fails
  the async cases this tool exists to handle.

### Cross-process consumer (CLI watcher, for agents to run alongside the target app)

A real physical channel is required here — see above for why the free OS channel isn't the
default choice. The cost lands as exactly one line in the target app, not at any call site:

- **Writer-side listener**: `Trace.Listeners.Add(myShippingListener)` once at startup. Every
  existing and future `Trace.WriteLine`/`Trace.TraceXxx` call in the app is captured from that
  point with zero further changes anywhere else.
- **Transport**: keep the writer side dependency-free too — `System.IO.Pipes.NamedPipeServerStream`
  (in-box BCL; runs over Unix domain sockets on Linux under the hood, so it isn't actually
  Windows-only despite the name) or a loopback `TcpListener`. Structured framing (e.g.
  newline-delimited JSON records carrying category/message/eventType/id) rather than flattened
  text, so the watcher doesn't have to parse anything back apart.
- **Watcher app**: a standalone console project, same shape as the existing
  `lazynats.AotProbe`/`lazynats.FeedLoadGen` precedent — not in the `.sln`, run via
  `dotnet run --project src/<name>`. Free to take on whatever dependencies are convenient
  (JSON, console formatting/coloring, CLI parsing) since it isn't subject to the host app's
  `PublishAot`/trim constraints the way the writer side is.

## Open questions

- Extraction: build this now under `src/` with a deliberately app-agnostic name and zero
  references to `lazynats.*` types (cheap, costs nothing if it never leaves), or start it as its
  own repo/package from day one?
- Filtering syntax on the consuming side: category glob (`App.Module.*`) vs. regex vs. both?
- Whether to standardize an optional stable `id` convention for the zero-footprint
  `Trace.WriteLine` path (no natural `id` slot exists there, unlike `TraceEvent`), for the subset
  of probes someone actually intends to assert on rather than eyeball.
- Naming, for both the shared library and the two consumer projects.
