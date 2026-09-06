## Context

`lazynats` is published with `PublishAot=true` and `InvariantGlobalization=true` (see `lazynats/lazynats.csproj`). Third-party libraries considered for the app (NATS client packages, System.Reactive, DI, and future candidates) are not guaranteed AOT-safe. `dotnet run`/`dotnet build` under the JIT will not surface many AOT failures: the JIT resolves reflection metadata that the AOT linker (ILC) strips away, so a library can look fine in normal development and only fail once natively published. There is currently no repeatable way to check a candidate library in isolation before wiring it into the real app.

## Goals / Non-Goals

**Goals:**
- Provide a minimal, repeatable harness (`lazynats.AotProbe`) for smoke-testing one library at a time under a real Native AOT publish + run, not just JIT.
- Keep each library's check isolated in its own file so failures are attributable and probes can be added incrementally as new libraries are evaluated.
- Make the check-loop a single script invocation (`test-aot.ps1`) that a developer runs by hand after adding a probe.
- Seed the harness with probes for the three libraries currently in question: NATS client, Rx, and DI (constructor injection specifically, since container reflection is a common AOT trip point).

**Non-Goals:**
- No automated/CI integration yet — the user will wire this into the real build pipeline later; this change only produces the manual dev-time tool.
- No Terminal.Gui probe in this change.
- No cross-platform (linux-x64) run in this change — the script accepts a `-Rid` parameter so that target can be exercised later on a dual-boot Linux box, but `win-x64` is the only RID actually validated now.
- Not a general-purpose test framework — no assertion library, no xunit/nunit (test frameworks add their own AOT-compat risk into the thing meant to validate AOT-compat, and reflection-based test discovery is itself a poor fit for a single-exe AOT probe).

## Decisions

**Separate project in the same solution, not a separate repo.**
`lazynats.AotProbe` is added as a second project to `lazynats.sln` alongside `lazynats`. This keeps it discoverable next to the app it's derisking for, and lets its `.csproj` mirror the real app's AOT-relevant settings (`net10.0`, `PublishAot=true`, `InvariantGlobalization=true`) so a probe result is representative of what `lazynats` itself would experience.

**Plain console app, no test framework.**
Probes are just methods called from `Program.cs`, not `[Fact]`/`[Test]` methods. Unit test runners typically rely on reflection-based discovery and their own runtime host, which is exactly the kind of thing that's fragile under AOT — using one here would risk testing the test framework's AOT-compatibility instead of the target library's. A plain `static void Run()`-per-probe keeps the harness itself simple enough to trust.

**One file per library under `Probes/`, called explicitly and in sequence from `Program.cs`.**
Each probe exercises a small but representative slice of the library's API — not just object construction, since AOT reflection failures typically show up on the actual call path (serialization, connection setup, DI resolution), not on `new`. `Program.cs` lists probes explicitly (no reflection-based auto-discovery of probes, which would undercut the point of the exercise) and accumulates over time as new libraries are evaluated.

**Per-probe try/catch with ✅/❌ output; one probe's failure does not stop the run.**
A single run should report on every registered probe, not halt at the first exception — otherwise adding a fourth probe risks losing signal on the first three every time it's run.

**Seed probes:**
- `NatsProbe`: connect to a NATS server, subscribe, publish with a `byte[]` payload, and confirm the message round-trips. It targets `nats://localhost:14442` — a dedicated probe-only port, not the default `4222` that `lazynats/Program.cs` uses — so this harness never collides with a "real" `nats-server` instance the developer might already have running. The probe should fail loudly and distinguishably if the connection itself fails, so a "no server running" failure isn't misread as an AOT problem.
- `RxProbe`: `Subject<T>`, publish, subscribe — deliberately trivial, since Rx's core `IObservable`/`IObserver` plumbing is not reflection-heavy; this is a baseline check rather than a deep one.
- `DiProbe`: register a small interface graph (at least one service with a constructor dependency on another registered service) and resolve it. `Microsoft.Extensions.DependencyInjection` resolves constructors via reflection, which is the actual AOT-risk surface being probed here — a no-dependency resolve wouldn't exercise that path.

**`test-aot.ps1` runs the full build → publish → execute loop, parameterized by RID, and owns the lifecycle of a throwaway `nats-server`.**
```
test-aot.ps1 [-Rid <rid>]   # default: win-x64
  1. dotnet build                                            # fast AOT/trim analyzer warnings
  2. dotnet publish -r <rid> -c Release --self-contained
       -p:PublishAot=true -o <out>                            # slow, authoritative ILC pass
  3. docker run -d --rm -p 14442:4222 nats:latest             # probe-only nats-server
     wait for port 14442 to accept connections
  4. run <out>/lazynats.AotProbe(.exe)                        # the real signal: runtime exceptions
  5. docker stop <container>  (always, via try/finally)       # even if the run throws
```
Step 1 is kept as a cheap early filter (the SDK auto-enables `EnableAotAnalyzer`/`EnableTrimAnalyzer` when `PublishAot=true` is set); step 2/4 is the step that actually proves or disproves AOT-safety, since analyzer warnings are necessarily incomplete relative to actually running the trimmed binary. Docker was chosen over requiring a `nats-server` binary on PATH because it was already present, running, and proven during this change's own verification — no extra manual install step for the developer. The script waits (polling the TCP port, bounded) rather than a fixed `Start-Sleep` guess, since a container that isn't ready yet would otherwise produce a false "connection failed" that looks identical to a real problem.

## Risks / Trade-offs

- **[Iteration speed]** Native AOT publish (ILC codegen) is slow (tens of seconds) compared to `dotnet run`. → Mitigation: `dotnet build`'s free analyzer pass runs first and catches known-bad patterns before paying the publish cost; this is accepted as inherent to what's being validated, not something to engineer around.
- **[External dependency for NatsProbe]** The NATS probe requires a reachable `nats-server`; `test-aot.ps1` now starts/stops one via Docker automatically, but a Docker daemon must be running on the machine. → Mitigation: the script fails fast with a clear message if the container can't start (e.g. Docker not running), and the probe's own failure output still distinguishes a connection failure from a trimming/reflection failure.
- **[Single-RID validation]** Only `win-x64` is actually run in this change; `linux-x64` AOT behavior is unverified until the user dual-boots to test it. → Mitigation: `-Rid` is a first-class script parameter now specifically so this is a config change later, not a rewrite.
- **[Harness accuracy]** A probe only proves what it exercises — a passing `DiProbe` doesn't prove every DI feature is AOT-safe, only constructor resolution for the shape tested. → Accepted: probes are meant to be small and representative, not exhaustive; deeper coverage is added incrementally if a specific feature becomes relevant.

## Migration Plan

Net-new project; nothing existing is modified or migrated. Rollout is: add `lazynats.AotProbe` to the solution, add the three seed probes, add `test-aot.ps1`, run it once against `win-x64` to confirm the harness itself works end-to-end. No rollback concerns — the project can be deleted with no effect on `lazynats`.

## Open Questions

- None blocking. Future: when CI integration happens (explicitly deferred by the user), the pass/fail-and-continue console output will need a machine-readable exit code or summary line — not needed for this change's manual-run workflow.
