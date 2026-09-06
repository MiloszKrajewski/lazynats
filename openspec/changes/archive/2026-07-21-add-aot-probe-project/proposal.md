## Why

lazynats is published Native AOT (`PublishAot=true`) so it can run without a .NET runtime installed. Third-party libraries are not reliably AOT-safe — reflection-based code paths that work fine under JIT (`dotnet run`/`dotnet build`) can throw at runtime once trimmed and natively compiled. There is currently no way to validate a candidate library's AOT-compatibility before wiring it into the real app; the only signal today is whether `lazynats` itself still runs, which is too coarse and too late.

## What Changes

- Add a new console project, `lazynats.AotProbe`, dedicated to smoke-testing third-party libraries under real Native AOT publish+run conditions (not just JIT).
- Add a `Probes/` folder holding one file per library, each exercising a small but representative slice of that library's API (not just "does it construct").
- Add an accumulating `Program.cs` that calls each probe in sequence, catching per-probe failures so one broken library doesn't prevent the rest from reporting.
- Seed three initial probes:
  - `NatsProbe`: connect, subscribe, publish (`byte[]` payload), receive.
  - `RxProbe`: `Subject<T>` publish/subscribe.
  - `DiProbe`: register-by-interface, resolve, with a small constructor-dependency chain to exercise ctor injection under trimming.
- Add `test-aot.ps1`: a script that builds, publishes (`-p:PublishAot=true --self-contained`), and runs the probe executable, with a `-Rid` parameter (default `win-x64`) so `linux-x64` can be targeted later without editing the script.
- No Terminal.Gui probe for now (out of scope for this change).

## Capabilities

### New Capabilities
- `aot-probe`: a standalone harness project + script for validating third-party library AOT-compatibility ahead of adopting them in `lazynats`.

### Modified Capabilities
- None. This does not change any existing runtime behavior of `lazynats` itself.

## Impact

- New project `lazynats.AotProbe/` added to `lazynats.sln`.
- New script `test-aot.ps1` at the solution or probe-project root.
- No changes to `lazynats/` application code or its dependencies.
- This harness is a dev-time tool; the user has indicated they will integrate it into the proper build/CI system later — that integration is explicitly out of scope for this change.
