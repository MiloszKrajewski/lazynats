## 1. Project Scaffolding

- [x] 1.1 Create `lazynats.AotProbe/lazynats.AotProbe.csproj` (console app) mirroring `lazynats.csproj`'s AOT settings: `TargetFramework=net10.0`, `PublishAot=true`, `InvariantGlobalization=true`, `ImplicitUsings`/`Nullable` enabled
- [x] 1.2 Add `lazynats.AotProbe` to `lazynats.sln`
- [x] 1.3 Add package references needed by the seed probes: NATS client package(s) matching `lazynats`' versions, `System.Reactive`, `Microsoft.Extensions.DependencyInjection`

## 2. Probe Harness

- [x] 2.1 Create `Probes/` folder
- [x] 2.2 Implement `Program.cs`: calls each registered probe in sequence, wraps each call in try/catch, prints a ✅/❌ line per probe (including exception message on failure), and continues to the next probe regardless of outcome

## 3. Seed Probes

- [x] 3.1 Implement `Probes/NatsProbe.cs`: connect to `nats://localhost:14442` (dedicated probe-only port, distinct from `lazynats`' default `4222`), subscribe to a subject, publish a message with a `byte[]` payload, assert the subscriber receives it; ensure a connection failure is reported distinctly from other exceptions (e.g. include exception type/message, don't swallow into a generic failure)
- [x] 3.2 Implement `Probes/RxProbe.cs`: create a `Subject<T>`, subscribe, publish a value, assert the subscriber received it
- [x] 3.3 Implement `Probes/DiProbe.cs`: define at least two interfaces/implementations where one's constructor depends on the other, register both in an `IServiceCollection`, build the provider, resolve the top-level service, assert its dependency was populated
- [x] 3.4 Register all three probes in `Program.cs`

## 4. Verification Script

- [x] 4.1 Create `test-aot.ps1` with a `-Rid` parameter defaulting to `win-x64`
- [x] 4.2 Script step: `dotnet build` against `lazynats.AotProbe` (fast AOT/trim analyzer pass)
- [x] 4.3 Script step: `dotnet publish -r <Rid> -c Release --self-contained -p:PublishAot=true` to an output folder
- [x] 4.4 Script step: run the published executable and stream its output to the console
- [x] 4.5 Script exits non-zero if build or publish fails (probe pass/fail output itself is informational per task 2.2, not a script exit condition, per design's deferred-CI note)

## 5. End-to-End Verification

- [x] 5.1 Start a local `nats-server`
- [x] 5.2 Run `./test-aot.ps1` and confirm all three probes report ✅ against the natively published, self-contained `win-x64` executable
- [x] 5.3 Temporarily stop the local `nats-server`, re-run, and confirm `NatsProbe` reports a clearly connection-related ❌ (not a generic/reflection-looking failure) while the other probes still run and report

## 6. Self-Managed Probe-Only NATS Server

- [x] 6.1 Update `Probes/NatsProbe.cs` to target port `14442` instead of the default `4222`
- [x] 6.2 Update `test-aot.ps1` to start a disposable `nats-server` via Docker (`-p 14442:4222`) before running the probe executable, wait for the port to accept connections before proceeding, and stop the container afterward via `try`/`finally` (so it stops even if a probe fails)
- [x] 6.3 Re-run `./test-aot.ps1` end-to-end with no `nats-server` running beforehand, confirm all three probes pass and the container is stopped/removed on completion
