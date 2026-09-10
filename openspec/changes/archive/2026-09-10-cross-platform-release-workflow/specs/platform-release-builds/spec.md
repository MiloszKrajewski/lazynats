## MODIFIED Requirements

### Requirement: Linux x64 native release archive via Docker
A `release-linux-x64` target SHALL produce a self-contained, Native AOT `linux-x64` build of
`lazynats`, zipped as `lazynats-<version>-linux-x64.zip` in `.output/`. When the executing host's
OS and CPU architecture already match `linux-x64` (i.e. the host is natively Linux x64), the
target SHALL publish directly on the host without Docker. Otherwise it SHALL fall back to
publishing inside a Docker container, so a non-Linux or non-x64 host can still produce the
artifact.

#### Scenario: Building on a native Linux x64 host
- **WHEN** `release-linux-x64` is executed on a host whose OS is Linux and whose CPU architecture
  is x64
- **THEN** `lazynats` is published self-contained for `linux-x64` with Native AOT enabled directly
  on the host, without starting a Docker container
- **AND** the result is zipped to `.output/lazynats-<version>-linux-x64.zip`

#### Scenario: Building from a host that isn't native Linux x64
- **WHEN** `release-linux-x64` is executed on a host that is not both Linux and x64
- **THEN** a Linux build container (.NET SDK plus the Native AOT Linux prerequisites) publishes
  `lazynats` self-contained for `linux-x64` with Native AOT enabled
- **AND** the result is zipped to `.output/lazynats-<version>-linux-x64.zip` on the host

#### Scenario: Docker unavailable and native doesn't apply
- **WHEN** `release-linux-x64` is executed on a host that is not native Linux x64, and Docker is
  not running or not installed
- **THEN** the target fails outright with Docker's own error surfaced, rather than silently
  skipping or producing a partial artifact

### Requirement: Linux arm64 native release archive via emulated Docker
A `release-linux-arm64` target SHALL produce a self-contained, Native AOT `linux-arm64` build of
`lazynats`, zipped as `lazynats-<version>-linux-arm64.zip` in `.output/`. When the executing host's
OS and CPU architecture already match `linux-arm64` (i.e. the host is natively Linux arm64), the
target SHALL publish directly on the host without Docker. Otherwise it SHALL fall back to
publishing inside a Docker container targeting `linux/arm64`, using QEMU emulation if the Docker
engine's own platform doesn't natively match `linux/arm64` either - a fact the target does not
assert in advance, since Docker resolves it transparently.

#### Scenario: Building on a native Linux arm64 host
- **WHEN** `release-linux-arm64` is executed on a host whose OS is Linux and whose CPU
  architecture is arm64
- **THEN** `lazynats` is published self-contained for `linux-arm64` with Native AOT enabled
  directly on the host, without starting a Docker container
- **AND** the result is zipped to `.output/lazynats-<version>-linux-arm64.zip`

#### Scenario: Building from a host that isn't native Linux arm64
- **WHEN** `release-linux-arm64` is executed on a host that is not both Linux and arm64
- **THEN** a `linux/arm64` Docker build container (.NET SDK plus the Native AOT Linux
  prerequisites) publishes `lazynats` self-contained for `linux-arm64` with Native AOT enabled,
  with Docker transparently deciding whether QEMU emulation is required
- **AND** the result is zipped to `.output/lazynats-<version>-linux-arm64.zip` on the host

#### Scenario: Docker unavailable and native doesn't apply
- **WHEN** `release-linux-arm64` is executed on a host that is not native Linux arm64, and Docker
  is not running or not installed
- **THEN** the target fails outright with Docker's own error surfaced, rather than silently
  skipping or producing a partial artifact

#### Scenario: arm64 emulation not registered
- **WHEN** `release-linux-arm64` falls back to Docker on a host whose Docker engine is running but
  does not have `linux/arm64` emulation registered (no QEMU/binfmt support for arm64)
- **THEN** the target fails with a message explaining that `linux/arm64` emulation is required and
  how to register it, rather than surfacing Docker's raw exec-format error

### Requirement: Explicit placeholders for unsupported platforms
`release-macos-arm64` SHALL exist and be discoverable as a build target. When the executing host's
OS and CPU architecture match `osx-arm64` (i.e. the host is natively macOS arm64), it SHALL
produce a self-contained, Native AOT `osx-arm64` build of `lazynats`, zipped as
`lazynats-<version>-macos-arm64.zip` in `.output/`, the same as the other platform targets.
Everywhere else it SHALL fail immediately with a message explaining that a macOS Native AOT build
requires a macOS arm64 host, rather than being silently absent or appearing to succeed.

#### Scenario: Building on a native macOS arm64 host
- **WHEN** `release-macos-arm64` is executed on a host whose OS is macOS and whose CPU
  architecture is arm64
- **THEN** `lazynats` is published self-contained for `osx-arm64` with Native AOT enabled directly
  on the host
- **AND** the result is zipped to `.output/lazynats-<version>-macos-arm64.zip`

#### Scenario: Invoking on a non-matching host
- **WHEN** `release-macos-arm64` is executed on a host that is not both macOS and arm64
- **THEN** it fails immediately with a message noting that a macOS Native AOT build requires a
  macOS arm64 host

### Requirement: Release archives exclude debug symbol files
`Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, and
`release-macos-arm64` SHALL each strip debug symbol files (`*.pdb` on Windows, `*.dbg` on Linux
and macOS) from the published output before zipping, so the resulting archive does not carry them.

#### Scenario: Zipping a published output
- **WHEN** any of `Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` zips its published output
- **THEN** the resulting zip contains no `.pdb` or `.dbg` files

### Requirement: Re-running a release target overwrites its zip
`Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, and
`release-macos-arm64` SHALL each overwrite their destination zip file if it already exists from a
previous run, rather than failing.

#### Scenario: Zip already exists from a prior run
- **WHEN** any of `Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` is run again while its destination zip file already exists in `.output/`
- **THEN** the target completes successfully and the zip is replaced with the new output
