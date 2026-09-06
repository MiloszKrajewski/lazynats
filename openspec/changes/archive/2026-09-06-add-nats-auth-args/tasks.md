## 1. Command-line options

- [x] 1.1 Add `user`, `password`, and `token` optional `string?` parameters to `RunAppAsync` in
      `Program.cs`, with triple-slash `<param>` doc comments (no short-form letter, matching the
      `nats` CLI) — mirror the existing `server` parameter's doc-comment style.
- [x] 1.2 Add a single `ResolveAuth(string? user, string? password, string? token)` static helper
      next to `ResolveServerUrl`, returning a `(string? User, string? Password, string? Token)`
      tuple: if `user`, `password`, and `token` are all `null`, return the environment-variable
      tuple (`NATS_USER`, `NATS_PASSWORD`, `NATS_TOKEN`, each possibly `null`); otherwise return
      `(user, password, token)` unchanged, with no per-field environment fallback — see
      `design.md`'s "Single `ResolveAuth` helper" decision.

## 2. Conditional auth wiring

- [x] 2.1 In `RunAppAsync`, after resolving user/password/token, build a `NatsAuthOpts?` per the
      precedence in `design.md`: token wins if present; else username+password if both present;
      else user-alone treated as token; else `null`.
- [x] 2.2 Pass the resulting `NatsAuthOpts?` as `AuthOpts` on the `NatsOpts` used to construct
      `NatsConnection`, only when non-null (leave `AuthOpts` at its default otherwise).

## 3. Verification

- [x] 3.1 Run `dotnet build src/lazynats.sln` to confirm it compiles.
- [x] 3.2 Manually verify against a real NATS server (or the dockerized one used by
      `test-aot.ps1`) configured with username/password auth: confirm `--user`/`--password`
      connects, a bare `--user` with no password is rejected by the server (proving it was sent
      as a token, not a username), and no-args still connects anonymously against an
      unauthenticated server.
- [x] 3.3 Manually verify against a real NATS server configured with token auth
      (`nats-server --auth <token>`): confirm `--token <token>` connects, and a bare
      `--user <token>` (no password) also connects using that same token — proving the
      user-alone-as-token path authenticates successfully end-to-end, not just that it's rejected
      against a mismatched (username/password) server as 3.2 showed.
- [x] 3.4 Manually verify the group-level precedence fix: launch with `--user`/`--password` given
      on the command line and an unrelated `NATS_TOKEN` set in the environment, against the
      username/password server from 3.2; confirm it connects via username/password, proving the
      stray env token is ignored rather than silently overriding the explicit command-line auth.
