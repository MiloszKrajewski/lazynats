## ADDED Requirements

### Requirement: Linux arm64 native release archive via emulated Docker
A `release-linux-arm64` target SHALL produce a self-contained, Native AOT `linux-arm64` build of
`lazynats` by running the publish inside a QEMU-emulated `linux/arm64` Docker container (so it
does not require an arm64 host), zipped as `lazynats-<version>-linux-arm64.zip` in `.output/`.

#### Scenario: Building from any host with Docker and arm64 emulation available
- **WHEN** `release-linux-arm64` is executed on a host with Docker running and `linux/arm64`
  emulation (QEMU/binfmt) registered
- **THEN** an emulated `linux/arm64` Linux build container (.NET SDK plus the Native AOT Linux
  prerequisites) publishes `lazynats` self-contained for `linux-arm64` with Native AOT enabled
- **AND** the result is zipped to `.output/lazynats-<version>-linux-arm64.zip` on the host

#### Scenario: Docker unavailable
- **WHEN** `release-linux-arm64` is executed and Docker is not running or not installed
- **THEN** the target fails outright with Docker's own error surfaced, rather than silently
  skipping or producing a partial artifact

#### Scenario: arm64 emulation not registered
- **WHEN** `release-linux-arm64` is executed on a host with Docker running but without
  `linux/arm64` emulation registered (no QEMU/binfmt support for arm64)
- **THEN** the target fails with a message explaining that `linux/arm64` emulation is required
  and how to register it, rather than surfacing Docker's raw exec-format error

## MODIFIED Requirements

### Requirement: Explicit placeholders for unsupported platforms
`release-macos-arm64` SHALL exist and be discoverable as a build target, and SHALL fail
immediately with a message explaining why the platform is not yet supported and what the future
implementation path is, rather than being silently absent or appearing to succeed.

#### Scenario: Invoking the macOS placeholder
- **WHEN** `release-macos-arm64` is executed
- **THEN** it fails immediately with a message noting that a macOS Native AOT build can only be
  produced on macOS hardware, which is not available to this pipeline

### Requirement: Release archives exclude debug symbol files
`Release`, `release-windows-x64`, `release-linux-x64`, and `release-linux-arm64` SHALL each strip
debug symbol files (`*.pdb` on Windows, `*.dbg` on Linux) from the published output before
zipping, so the resulting archive does not carry them.

#### Scenario: Zipping a published output
- **WHEN** any of `Release`, `release-windows-x64`, `release-linux-x64`, or `release-linux-arm64`
  zips its published output
- **THEN** the resulting zip contains no `.pdb` or `.dbg` files

### Requirement: Re-running a release target overwrites its zip
`Release`, `release-windows-x64`, `release-linux-x64`, and `release-linux-arm64` SHALL each
overwrite their destination zip file if it already exists from a previous run, rather than
failing.

#### Scenario: Zip already exists from a prior run
- **WHEN** any of `Release`, `release-windows-x64`, `release-linux-x64`, or `release-linux-arm64`
  is run again while its destination zip file already exists in `.output/`
- **THEN** the target completes successfully and the zip is replaced with the new output

### Requirement: Release targets are independently invoked
`release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, and `release-macos-arm64`
SHALL each be invocable on their own and SHALL NOT be added as a dependency of the `Release`
target or of each other. Each SHALL be ordered to run after `Release` (via an ordering hint, not
a dependency) so that when both are scheduled in the same invocation, `Release` runs first.

#### Scenario: Running Release does not trigger the platform targets
- **WHEN** the `Release` target is executed
- **THEN** none of `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` are executed as a side effect

#### Scenario: Invoking a platform target alone does not trigger Release
- **WHEN** `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` is executed on its own
- **THEN** `Release` is not executed as a side effect

#### Scenario: Scheduling Release together with a platform target orders Release first
- **WHEN** `Release` and one of `release-windows-x64`, `release-linux-x64`,
  `release-linux-arm64`, or `release-macos-arm64` are both requested in the same invocation
- **THEN** `Release` executes before the platform target
