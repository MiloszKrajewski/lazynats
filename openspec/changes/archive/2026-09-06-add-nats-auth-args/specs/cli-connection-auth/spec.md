## ADDED Requirements

### Requirement: Auth option resolution precedence
The application SHALL resolve `--user`, `--password`, and `--token` together as a single group,
not independently:
- If any one of the three command-line options is given, the group resolves entirely from the
  command line: each option not given resolves to `null` — none of `NATS_USER`, `NATS_PASSWORD`,
  `NATS_TOKEN` is consulted, even for an option the command line left unset.
- If none of the three command-line options is given, the group resolves entirely from the
  environment: `--user` from `NATS_USER`, `--password` from `NATS_PASSWORD`, `--token` from
  `NATS_TOKEN`, each individually `null` if its variable is unset.

#### Scenario: Command-line option only
- **WHEN** the application is launched with `--token abc123` and `NATS_TOKEN` is unset
- **THEN** the token option resolves to `abc123`

#### Scenario: Environment variable only
- **WHEN** the application is launched without `--user`, `--password`, or `--token`, and
  `NATS_USER` is set to `alice`
- **THEN** the user option resolves to `alice`

#### Scenario: Command-line option overrides its own environment variable
- **WHEN** the application is launched with `--password secret` and `NATS_PASSWORD` set to a
  different value
- **THEN** the password option resolves to `secret` (the command-line value), ignoring
  `NATS_PASSWORD`

#### Scenario: A command-line auth option suppresses unrelated environment variables too
- **WHEN** the application is launched with `--user alice --password secret` (no `--token`), and
  `NATS_TOKEN` is set in the environment to some leftover value
- **THEN** the token option resolves to `null` — `NATS_TOKEN` is not consulted, because
  `--user`/`--password` were given on the command line — so authentication proceeds as
  username/password rather than being silently overridden by the ambient token

#### Scenario: Neither set
- **WHEN** the application is launched with none of `--user`/`--password`/`--token` and none of
  `NATS_USER`/`NATS_PASSWORD`/`NATS_TOKEN` set
- **THEN** all three options resolve to unset (`null`)

### Requirement: Conditional authentication before connecting
The application SHALL determine an authentication mode from the resolved user, password, and
token values before constructing the `NatsConnection`, applying the following rules in order:

1. If a token value is resolved (from `--token`/`NATS_TOKEN`), the connection SHALL authenticate
   using that value as a bearer token, regardless of any resolved user or password value.
2. Otherwise, if both a user value and a password value are resolved, the connection SHALL
   authenticate using username/password auth with those values.
3. Otherwise, if a user value is resolved but no password value is resolved, the connection SHALL
   authenticate using that user value as a bearer token.
4. Otherwise (including the case where only a password value is resolved with no user and no
   token), the connection SHALL be unauthenticated, identical to today's behavior.

#### Scenario: Token takes precedence over user and password
- **WHEN** the application is launched with `--token abc123 --user alice --password secret`
- **THEN** it connects authenticating with token `abc123`, ignoring the user and password values

#### Scenario: Username and password auth
- **WHEN** the application is launched with `--user alice --password secret` and no `--token`
- **THEN** it connects authenticating with username `alice` and password `secret`

#### Scenario: Bare user value treated as token
- **WHEN** the application is launched with `--user abc123` and neither `--password` nor
  `--token` is set
- **THEN** it connects authenticating with token `abc123`

#### Scenario: Password alone is ignored
- **WHEN** the application is launched with `--password secret` and neither `--user` nor
  `--token` is set
- **THEN** it connects unauthenticated, identical to launching with none of the three options set

#### Scenario: No auth options given
- **WHEN** the application is launched with none of `--user`/`--password`/`--token` set (and none
  of the corresponding environment variables set)
- **THEN** it connects unauthenticated, identical to today's behavior

### Requirement: `--user`, `--password`, `--token` command-line options
The application SHALL expose `--user <value>`, `--password <value>`, and `--token <value>`
command-line options, each accepting a single string value, with no short-form equivalents.

#### Scenario: Long form only
- **WHEN** the application is launched with `--user alice`
- **THEN** the option value `alice` is used as the user argument, with no `-u` short form
  accepted
