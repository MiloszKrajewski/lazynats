## Context

`src/lazynats/Program.cs` is a plain top-level-statements file: no `Main` method, no argument
handling, `args` is available but unused. The one hardcoded line that needs to become
configurable is:

```csharp
var connection = new NatsConnection(new NatsOpts { Url = "nats://localhost:4222" });
```

The project has standardized on `ConsoleAppFramework` (Cysharp) as the CLI-args framework of
choice. It is a Roslyn source generator — it inspects the method/lambda passed to
`ConsoleApp.Run`/`RunAsync` at compile time and emits parsing code with no reflection, which
matches the `PublishAot=true` constraint in `CLAUDE.md`. It is not yet referenced by any project
in the solution.

## Goals / Non-Goals

**Goals:**
- Accept `--server <url>` (and short form `-s <url>`) on the command line.
- Resolve the effective server URL with precedence `--server` > `NATS_URL` env var > default
  `nats://localhost:4222`.
- Keep the change scoped to `src/lazynats` (the shipping app), not `lazynats.FeedLoadGen` /
  `lazynats.AotProbe`.

**Non-Goals:**
- No other new CLI options (auth credentials, TLS, etc.) — just the server URL.
- No subcommands. `lazynats` remains a single root command; `ConsoleAppFramework` is used purely
  for root-level option parsing, not its multi-command/class-registration features.
- Not changing `NATS_URL` handling inside `NATS.Client.Core` itself (it has none today —
  confirmed by grep) — resolution is entirely app-side, before constructing `NatsOpts`.

## Decisions

**Pass a named method reference to `ConsoleApp.RunAsync`, not an inline lambda wrapping the
whole body.**
`Program.cs`'s startup body is ~50 lines. Extracting it into a local
`static async Task RunAppAsync(string? server = null)` function and calling
`await ConsoleApp.RunAsync(args, RunAppAsync);` at the top keeps the diff to "hoist body into a
method + one new call", with no re-indentation of the existing logic. This also mirrors the
existing pattern in the same file where `ApplyColorTheme()` is already a local static function
called by name. Alternative considered: wrap the whole body inline as
`ConsoleApp.RunAsync(args, async (string? server) => { ... })` — rejected only because it forces
re-indenting the entire file for no behavioral difference.

**Short alias via XML doc comment, not an attribute.**
`ConsoleAppFramework` reads short/long aliases from the `<param>` tag on the command method's XML
doc comment (e.g. `/// <param name="server">-s, NATS server URL.</param>`), rather than a
`[Option]`-style attribute. This is the documented mechanism (confirmed against current
Cysharp/ConsoleAppFramework docs via context7) and needs no extra package feature — just a doc
comment on `RunAppAsync`.

**Precedence resolution is a tiny local static function, independent of CAF.**
```csharp
static string ResolveServerUrl(string? server) =>
    server ?? Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
```
`ConsoleAppFramework` only needs to hand back a nullable `string? server = null` parameter; the
precedence chain is plain framework-agnostic C#, which keeps it trivially readable and not
coupled to CAF's parsing behavior.

**Accept CAF's free `-h`/`--help` and `--version` behavior.**
`ConsoleApp.Run`/`RunAsync` auto-generates help/version output. Not something to suppress —
consistent with how other CLIs behave — but noted here since it's a side effect nobody asked for
explicitly.

## Risks / Trade-offs

- [Unverified: whether XML-doc `<param>` alias parsing applies identically when the command
  method is passed directly to `ConsoleApp.RunAsync` (this design), versus the documented example
  which registers the method via a class and `app.Add<T>()`] → Mitigation: smoke-test `-s` and
  `--server` against a real build during implementation (task list includes this); if the alias
  is not honored in this registration style, fall back to registering a single-method command
  class via `ConsoleApp.Create().Add<T>()` instead of the bare `ConsoleApp.RunAsync` call.
- [`NatsConnection`/`ConnectAsync()` failure on a bad `--server` value surfaces as whatever
  exception `NATS.Client.Core` throws today] → Mitigation: none needed for this change — error
  handling on connect failure is unchanged/out of scope; today's hardcoded URL has the same
  failure behavior.

## Migration Plan

Not applicable — additive CLI option with a default that preserves current behavior exactly
(`nats://localhost:4222` when neither `--server` nor `NATS_URL` is set). No rollback concerns.

## Open Questions

- None blocking. The alias-registration-style question above is resolved by a fallback path
  already captured in Risks/Trade-offs, not left open.
