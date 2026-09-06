## Why

`lazynats` currently connects anonymously — there's no way to point it at a NATS server that
requires username/password or token authentication without editing code. The `nats` CLI already
solves this with `--user`/`--password`/`--token` (plus matching `NATS_USER`/`NATS_PASSWORD`/
`NATS_TOKEN` env vars), so `lazynats` should offer the same shape rather than inventing its own.

## What Changes

- Add `--user`, `--password`, and `--token` command-line options, each falling back to the
  `NATS_USER`, `NATS_PASSWORD`, `NATS_TOKEN` environment variables respectively, mirroring the
  existing `--server`/`NATS_URL` precedence (`cli-server-url`).
- Conditionally build a `NatsAuthOpts` and attach it to the `NatsConnection` before connecting:
  - `--token` (or `NATS_TOKEN`) present → token auth, taking precedence over user/password.
  - `--user` and `--password` both present (no token) → username/password auth.
  - `--user` present alone (no password, no token) → treat the user value as a bearer token
    (matches the `nats` CLI's own `--user` help text: "Username or Token").
  - `--password` present alone (no user, no token) → ignored; connection stays unauthenticated.
  - None present → unauthenticated connection, unchanged from today.

## Capabilities

### New Capabilities
- `cli-connection-auth`: command-line/env-var configuration of NATS username/password/token
  authentication, resolved and applied before connecting.

### Modified Capabilities
(none — `cli-server-url`'s own requirements are unchanged; this change only adds sibling options)

## Impact

- `src/lazynats/Program.cs`: new `Resolve*` helpers for user/password/token (mirroring
  `ResolveServerUrl`) and conditional `NatsAuthOpts` construction passed into `NatsOpts` before
  `ConnectAsync()`.
- No changes to `NatsOpts`/`NatsAuthOpts` themselves (NATS.Client.Core 3.2.0, already referenced)
  or to any UI code.
