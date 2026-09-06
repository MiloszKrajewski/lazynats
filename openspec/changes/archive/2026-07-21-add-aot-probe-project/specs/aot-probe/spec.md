## ADDED Requirements

### Requirement: AOT-Mirrored Probe Project
The system SHALL provide a standalone console project, `lazynats.AotProbe`, configured with the same AOT-relevant settings as `lazynats` (`PublishAot=true`, `InvariantGlobalization=true`, matching target framework), so that a probe result under Native AOT publish is representative of what `lazynats` would experience with the same library.

#### Scenario: Probe project publishes under Native AOT
- **WHEN** `lazynats.AotProbe` is published with `-p:PublishAot=true --self-contained`
- **THEN** the publish succeeds and produces a self-contained native executable, without requiring a .NET runtime to be installed to run it

### Requirement: Isolated Per-Library Probes
The system SHALL organize AOT checks as one file per library under a `Probes/` folder, each exercising a representative slice of that library's API (beyond mere object construction), so that a failure is attributable to a specific library and new libraries can be added incrementally without affecting existing probes.

#### Scenario: Adding a new probe does not require changing existing probes
- **WHEN** a new library's probe file is added under `Probes/` and registered in `Program.cs`
- **THEN** existing probe files and their behavior are unchanged

### Requirement: Resilient Sequential Probe Execution
The system SHALL run all registered probes in sequence from `Program.cs`, catching each probe's exceptions individually, so that one probe's failure does not prevent the remaining probes from running and reporting.

#### Scenario: One probe throws, others still run
- **WHEN** one probe raises an exception during execution
- **THEN** the remaining registered probes still execute, and the failing probe's exception does not crash the process

#### Scenario: Per-probe pass/fail is reported
- **WHEN** the probe executable finishes running all registered probes
- **THEN** the console output shows a pass or fail indicator for each individual probe

### Requirement: NATS Client Probe
The system SHALL include a probe that connects to a NATS server on a dedicated probe-only port (`14442`, distinct from the default `4222` `lazynats` itself uses), subscribes to a subject, publishes a message with a `byte[]` payload, and confirms the message is received, so that the NATS client library's connect/subscribe/publish path is validated under AOT without colliding with a "real" `nats-server` instance that may already be running.

#### Scenario: NATS round-trip succeeds under AOT
- **WHEN** the NATS probe runs against a reachable `nats-server` on port `14442`
- **THEN** it connects, subscribes, publishes a `byte[]` payload, and confirms receipt of that message without a reflection-related exception

#### Scenario: NATS connection failure is distinguishable from an AOT failure
- **WHEN** the NATS probe runs and no NATS server is reachable on port `14442`
- **THEN** the reported failure clearly indicates a connection failure rather than being conflated with a trimming or reflection error

### Requirement: Self-Managed Probe-Only NATS Server
The system SHALL start a disposable `nats-server` (via Docker, bound to port `14442`) before running the probes and stop it afterward regardless of whether the probe run succeeds, so that the developer does not need to manually manage a NATS server instance and it never conflicts with a `nats-server` the developer may already be running for other purposes.

#### Scenario: Server starts before probes run and stops after, even on failure
- **WHEN** `test-aot.ps1` runs and a probe throws or the probe executable exits non-zero
- **THEN** the Docker-managed `nats-server` container is still stopped before the script exits

#### Scenario: Script fails fast if the NATS server cannot start
- **WHEN** the Docker-managed `nats-server` container fails to start (e.g. Docker is not running)
- **THEN** `test-aot.ps1` reports the failure clearly and exits without attempting to run the probes

### Requirement: Rx Library Probe
The system SHALL include a probe that creates a `Subject<T>`, publishes a value, and confirms a subscriber receives it, so that Rx's core observable/observer path is validated under AOT.

#### Scenario: Rx publish/subscribe succeeds under AOT
- **WHEN** the Rx probe runs
- **THEN** a value published to a `Subject<T>` is received by a subscriber without a reflection-related exception

### Requirement: Dependency Injection Constructor Resolution Probe
The system SHALL include a probe that registers at least two services by interface, where one service's constructor depends on another registered interface, and resolves the dependent service, so that constructor-injection resolution is validated under AOT (a common reflection-based trimming risk for DI containers).

#### Scenario: DI resolves a constructor dependency chain under AOT
- **WHEN** the DI probe registers services by interface with a constructor dependency between them and resolves the top-level service
- **THEN** resolution succeeds and the resolved instance's dependency is populated, without a reflection-related exception

### Requirement: Configurable AOT Verification Script
The system SHALL provide a PowerShell script, `test-aot.ps1`, that builds the probe project, publishes it for Native AOT with a self-contained deployment, and runs the resulting executable, accepting a RID parameter that defaults to `win-x64`.

#### Scenario: Default run targets win-x64
- **WHEN** `test-aot.ps1` is run with no parameters
- **THEN** it builds, publishes for `win-x64` with `-p:PublishAot=true --self-contained`, and runs the resulting executable

#### Scenario: RID is overridable for future cross-platform checks
- **WHEN** `test-aot.ps1` is run with a different RID (e.g. `linux-x64`)
- **THEN** the publish step targets that RID instead of the default, without requiring script changes
