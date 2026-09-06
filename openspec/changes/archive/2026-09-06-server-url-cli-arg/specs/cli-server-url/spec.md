## ADDED Requirements

### Requirement: Server URL configuration precedence
The application SHALL determine the NATS server URL to connect to at startup using the following
precedence, highest first: the `--server`/`-s` command-line option, then the `NATS_URL`
environment variable, then the literal default `nats://localhost:4222`. Exactly one resolved URL
SHALL be used to construct the `NatsConnection`.

#### Scenario: No argument, no environment variable
- **WHEN** the application is launched with neither `--server`/`-s` nor `NATS_URL` set
- **THEN** it connects using `nats://localhost:4222`

#### Scenario: Environment variable only
- **WHEN** the application is launched without `--server`/`-s` but with `NATS_URL` set to a URL
- **THEN** it connects using the `NATS_URL` value

#### Scenario: Command-line argument only
- **WHEN** the application is launched with `--server <url>` (or `-s <url>`) and `NATS_URL` is
  unset
- **THEN** it connects using `<url>`

#### Scenario: Both argument and environment variable set
- **WHEN** the application is launched with `--server <url>` and `NATS_URL` is also set to a
  different value
- **THEN** it connects using `<url>` (the command-line argument), ignoring `NATS_URL`

### Requirement: `--server` command-line option
The application SHALL expose a `--server <url>` command-line option accepting a NATS server URL
as its value, with `-s` accepted as an equivalent short form of the same option.

#### Scenario: Long form
- **WHEN** the application is launched with `--server nats://example.com:4222`
- **THEN** the option value `nats://example.com:4222` is used as the server URL argument

#### Scenario: Short form
- **WHEN** the application is launched with `-s nats://example.com:4222`
- **THEN** the option value `nats://example.com:4222` is used as the server URL argument,
  identically to the long form
