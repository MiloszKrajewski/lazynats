## 1. Dependency

- [x] 1.1 Add `ConsoleAppFramework` `PackageReference` to `src/lazynats/lazynats.csproj` (pin an
      exact version).

## 2. Entry point restructuring

- [x] 2.1 In `src/lazynats/Program.cs`, extract the existing top-level statement body (from
      `var connection = ...` through `app.Run<MainWindow>().Dispose();`) into a local
      `static async Task RunAppAsync(string? server = null)` function.
- [x] 2.2 Replace the top of the file with `await ConsoleApp.RunAsync(args, RunAppAsync);`.
- [x] 2.3 Add the XML doc comment on `RunAppAsync` defining the `-s` short alias for `server`
      (`/// <param name="server">-s, NATS server URL.</param>`), per design.md.

## 3. Server URL resolution

- [x] 3.1 Add a local `static string ResolveServerUrl(string? server)` function implementing the
      precedence `server ?? Environment.GetEnvironmentVariable("NATS_URL") ??
      "nats://localhost:4222"`.
- [x] 3.2 Replace the hardcoded `Url = "nats://localhost:4222"` in the `NatsOpts` construction
      with `Url = ResolveServerUrl(server)`.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 4.2 Smoke-test against a real NATS server: launch with no args/env (connects to
      `nats://localhost:4222`), with `--server <url>`, with `-s <url>`, and with `NATS_URL` set
      alone — confirm precedence order from the spec, and confirm the `-s` alias is honored given
      `RunAppAsync` is passed directly to `ConsoleApp.RunAsync` rather than registered via
      `Add<T>()` (the open risk flagged in design.md).
- [x] 4.3 If 4.2 shows `-s` is not honored in this registration style, switch to registering
      `RunAppAsync` (or an equivalent single-method command class) via
      `ConsoleApp.Create().Add<T>()` instead, per the design.md fallback, and re-verify.
      (Not needed: `-s` was honored directly against the bare `RunAppAsync` method reference —
      confirmed via `--help` output and live smoke tests.)
- [x] 4.4 Confirm `--help` output lists both `--server` and `-s` with the expected default value.
