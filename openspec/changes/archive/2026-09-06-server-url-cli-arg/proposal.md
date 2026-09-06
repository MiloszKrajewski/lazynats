## Why

`Program.cs` hardcodes `nats://localhost:4222` as the NATS server address, so the only way to
point `lazynats` at a different server is to edit and rebuild the source. Every other NATS CLI
tool (and the `nats` CLI already vendored in `.bin/`) accepts a server URL on the command line;
lazynats should too.

## What Changes

- Add a `--server`/`-s <url>` command-line option, parsed via `ConsoleAppFramework` (already the
  project's chosen CLI-args framework; source-generator based, so it stays AOT-safe under
  `PublishAot`).
- Resolve the effective NATS server URL with precedence: `--server` argument > `NATS_URL`
  environment variable > `nats://localhost:4222` default.
- Restructure `Program.cs`'s top-level statements so the existing startup body runs as the
  command delegate passed to `ConsoleApp.RunAsync`, rather than unconditional top-level code.

## Capabilities

### New Capabilities
- `cli-server-url`: command-line and environment-variable configuration of the NATS server URL
  the app connects to on startup, with defined precedence and a documented default.

### Modified Capabilities
(none — no existing spec covers startup/CLI argument handling)

## Impact

- `src/lazynats/lazynats.csproj`: new `ConsoleAppFramework` package reference.
- `src/lazynats/Program.cs`: entry point restructured to route through `ConsoleApp.RunAsync`;
  server URL resolution extracted from the hardcoded `NatsOpts` literal.
- No changes to `src/lazynats.FeedLoadGen` or `src/lazynats.AotProbe` — they are separate
  throwaway tools with their own hardcoded NATS URLs, out of scope here.
