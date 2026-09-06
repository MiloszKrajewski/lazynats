## Context

`Program.cs` already resolves the NATS server URL via `ResolveServerUrl(server)` — cmd-line arg,
then `NATS_URL` env var, then a literal default — before constructing `NatsOpts` (see
`cli-server-url` spec). This change adds the same arg-then-env resolution for `--user`,
`--password`, and `--token`, then conditionally builds a `NATS.Client.Core.NatsAuthOpts` and
assigns it to `NatsOpts.AuthOpts` before `ConnectAsync()`. `NatsAuthOpts` (already available via
the referenced `NATS.Client.Core` 3.2.0) exposes `Username`, `Password`, and `Token` properties
that map directly onto this.

## Goals / Non-Goals

**Goals:**
- Let a user authenticate against a NATS server that requires username/password or token auth,
  using the same flag/env-var names as the `nats` CLI (`--user`/`NATS_USER`,
  `--password`/`NATS_PASSWORD`, `--token`/`NATS_TOKEN`).
- Keep the resolution and precedence rules simple and explicit, matching the decisions already
  made for `--server`/`NATS_URL`.

**Non-Goals:**
- No support for `--creds`, `--nkey`, TLS client certs, or any other `nats` CLI auth flag —
  scoped strictly to user/password/token per the proposal.
- No persistence (e.g. no config file or context concept like `nats context`) — resolved fresh
  on every launch, same as the server URL today.

## Decisions

- **Single `ResolveAuth` helper returning a `(string? User, string? Password, string? Token)`
  tuple**, resolved as a group rather than three independent `arg ?? env` lookups:
  - If all three command-line values (`user`, `password`, `token`) are `null` — no auth flag
    given at all — it returns the environment-variable tuple: `(NATS_USER, NATS_PASSWORD,
    NATS_TOKEN)`, each individually possibly still `null` if unset.
  - Otherwise (at least one of `--user`/`--password`/`--token` given), it returns the
    command-line tuple unchanged, with **no per-field environment-variable fallback** — a
    partially-specified command line (e.g. `--user` alone) is taken as complete, not topped up
    from the environment.
  - **Why not per-field independent resolution** (the originally considered approach): it let an
    ambient `NATS_TOKEN` left over in the shell environment silently override an explicit
    `--user`/`--password` pair typed on the command line — token wins over user+password
    regardless of source, so there was no way to suppress a stale env token short of unsetting
    it. Group-level fallback closes that gap: any command-line auth flag takes full, exclusive
    control of auth resolution for that run; env vars only apply when the command line specifies
    none of the three at all.
- **Precedence: token > user+password > user-alone > nothing**, applied to whichever tuple (CLI
  or env) was selected above, decided explicitly with the user during exploration:
  - `token` resolved (non-null) → `AuthOpts = new NatsAuthOpts { Token = token }`, regardless of
    whatever `user`/`password` also resolved to.
  - Else `user` and `password` both resolved → `AuthOpts = new NatsAuthOpts { Username = user,
    Password = password }`.
  - Else `user` resolved alone (no password, no token) → `AuthOpts = new NatsAuthOpts { Token =
    user }`. This mirrors the `nats` CLI's own `--user` help text ("Username or Token") rather
    than inventing new semantics.
  - Else (nothing, or `password` alone with no `user`) → `AuthOpts` left `null` — an
    unauthenticated connection, unchanged from today. `password` with no `user` is meaningless
    under both this scheme and plain NATS username/password auth, so it's silently ignored rather
    than erroring.
- **No short flags** (`-u`/`-p`/`-t`) for the three new options, matching the `nats` CLI itself —
  it only gives `-s` a short form among its global connection flags.
- **Alternative considered and rejected**: exposing `--creds`/`NatsCreds` as a fourth option now.
  Rejected because the proposal explicitly scoped this to user/password/token; credentials-file
  auth can be a separate follow-up change if needed.

## Risks / Trade-offs

- **[Risk]** The user-alone-means-token overload is implicit and could surprise someone who
  passes `--user` expecting it to require a paired `--password`. → **Mitigation**: documented in
  the spec's scenarios and in the `--user` option's help text (mirroring the `nats` CLI's own
  wording), so `--help` output makes the behavior discoverable.
- **[Risk]** Passing secrets via `--password`/`--token` command-line args exposes them in shell
  history / process listings. → **Mitigation**: this is the same trade-off the `nats` CLI itself
  accepts (and mitigates via env vars, which `lazynats` also supports here); out of scope to
  solve differently in this change.
- **[Trade-off]** Group-level fallback means a partial command line (e.g. `--user` alone) can no
  longer pick up a sibling value from the environment (e.g. `NATS_PASSWORD`) — that combo is no
  longer possible now that any command-line auth flag opts the whole group out of env lookup.
  Accepted deliberately: it's what closes the stale-env-token override problem described above,
  and mixing one secret from the command line with another from the environment was never a
  documented/relied-upon capability.

## Verification

Manually verified via `tmux`-driven runs of `dotnet run --project src/lazynats` against throwaway
dockerized `nats-server` instances (see `tasks.md` 3.2-3.4):
- Username/password server (`nats-server --user alice --pass secret`): `--user alice --password
  secret` connects; bare `--user alice` (no password) is rejected with `Authorization Violation`
  (proving it was sent as a token, not a username); no auth args at all is also rejected
  (unauthenticated, as expected against an auth-required server).
- Token server (`nats-server --auth mytoken123`): `--token mytoken123` connects; bare
  `--user mytoken123` (no password) also connects using that same value as a token — confirming
  the user-alone-as-token path authenticates end-to-end, not just that it's rejected elsewhere.
- Precedence fix regression check: `--user alice --password secret` on the command line with an
  unrelated `NATS_TOKEN` set in the environment still connects via username/password against the
  username/password server, confirming the stray env token is ignored rather than silently
  overriding the explicit command-line auth (the problem this change's precedence rework fixes).

## Migration Plan

No migration — purely additive. Existing invocations with no auth flags/env vars behave exactly
as before (anonymous connection).
