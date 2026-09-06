# platform-release-builds Specification

## Purpose
Defines the Nuke build targets that produce release artifacts for `lazynats`: the
framework-dependent default `Release` build, plus OS/arch-specific native-AOT release archives
(Windows x64, Linux x64 via Docker) and explicit placeholder targets for combinations not yet
supported (Linux arm64, macOS arm64), each following the
`lazynats-<version>-<system>-<arch>.zip` naming convention in `.output/`.

## Requirements

### Requirement: Framework-dependent Release build
The `Release` target SHALL publish `lazynats` as a framework-dependent, non-self-contained build
(no forced Native AOT), producing `lazynats-<version>.zip` in `.output/`.

#### Scenario: Release runs without a RID or AOT
- **WHEN** the `Release` target is executed
- **THEN** the published output is framework-dependent (requires a matching .NET runtime to run,
  is not self-contained, and is not a Native AOT binary)
- **AND** the zip artifact is named `lazynats-<version>.zip`

### Requirement: Windows x64 native release archive
A `release-windows-x64` target SHALL produce a self-contained, Native AOT `win-x64` build of
`lazynats`, zipped as `lazynats-<version>-windows-x64.zip` in `.output/`, and SHALL only run on a
Windows host.

#### Scenario: Building on Windows
- **WHEN** `release-windows-x64` is executed on a Windows host
- **THEN** `lazynats` is published self-contained for `win-x64` with Native AOT enabled
- **AND** the result is zipped to `.output/lazynats-<version>-windows-x64.zip`

#### Scenario: Attempted on a non-Windows host
- **WHEN** `release-windows-x64` is executed on a non-Windows host
- **THEN** the target fails immediately with a message explaining that Windows AOT builds require
  a Windows host
- **AND** no partial or incorrect artifact is produced

### Requirement: Linux x64 native release archive via Docker
A `release-linux-x64` target SHALL produce a self-contained, Native AOT `linux-x64` build of
`lazynats` by running the publish inside a Docker container (so it does not require a Linux
host), zipped as `lazynats-<version>-linux-x64.zip` in `.output/`.

#### Scenario: Building from any host with Docker available
- **WHEN** `release-linux-x64` is executed on a host with Docker running
- **THEN** a Linux build container (.NET SDK plus the Native AOT Linux prerequisites) publishes
  `lazynats` self-contained for `linux-x64` with Native AOT enabled
- **AND** the result is zipped to `.output/lazynats-<version>-linux-x64.zip` on the host

#### Scenario: Docker unavailable
- **WHEN** `release-linux-x64` is executed and Docker is not running or not installed
- **THEN** the target fails outright with Docker's own error surfaced, rather than silently
  skipping or producing a partial artifact

### Requirement: Explicit placeholders for unsupported platforms
`release-linux-arm64` and `release-macos-arm64` targets SHALL exist and be discoverable as build
targets, and SHALL fail immediately with a message explaining why the platform is not yet
supported and what the future implementation path is, rather than being silently absent or
appearing to succeed.

#### Scenario: Invoking the Linux arm64 placeholder
- **WHEN** `release-linux-arm64` is executed
- **THEN** it fails immediately with a message noting that arm64 support requires either a
  QEMU-emulated build container or a cross-compile toolchain, neither of which is implemented yet

#### Scenario: Invoking the macOS placeholder
- **WHEN** `release-macos-arm64` is executed
- **THEN** it fails immediately with a message noting that a macOS Native AOT build can only be
  produced on macOS hardware, which is not available to this pipeline

### Requirement: Release archives exclude debug symbol files
`Release`, `release-windows-x64`, and `release-linux-x64` SHALL each strip debug symbol files
(`*.pdb` on Windows, `*.dbg` on Linux) from the published output before zipping, so the resulting
archive does not carry them.

#### Scenario: Zipping a published output
- **WHEN** any of `Release`, `release-windows-x64`, or `release-linux-x64` zips its published
  output
- **THEN** the resulting zip contains no `.pdb` or `.dbg` files

### Requirement: Re-running a release target overwrites its zip
`Release`, `release-windows-x64`, and `release-linux-x64` SHALL each overwrite their destination
zip file if it already exists from a previous run, rather than failing.

#### Scenario: Zip already exists from a prior run
- **WHEN** any of `Release`, `release-windows-x64`, or `release-linux-x64` is run again while its
  destination zip file already exists in `.output/`
- **THEN** the target completes successfully and the zip is replaced with the new output

### Requirement: Release targets are independently invoked
`release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, and `release-macos-arm64`
SHALL each be invocable on their own and SHALL NOT be added as a dependency of the `Release`
target or of each other.

#### Scenario: Running Release does not trigger the platform targets
- **WHEN** the `Release` target is executed
- **THEN** none of `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` are executed as a side effect
