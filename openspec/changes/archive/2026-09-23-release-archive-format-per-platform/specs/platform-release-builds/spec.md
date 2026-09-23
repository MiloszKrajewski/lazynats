## ADDED Requirements

### Requirement: Archive format follows the target platform
Each release target SHALL pick its archive format by platform: `Release` (noarch) and
`release-windows-x64` SHALL produce a `.zip`; `release-linux-x64`, `release-linux-arm64` and
`release-macos-arm64` SHALL produce a gzip-compressed tar archive with the `.tgz` extension. Both
formats SHALL be written through the build's existing archiving dependency (Nuke's
`AbsolutePath.CompressTo`), without adding a new compression library or a hand-written archiver.

#### Scenario: Windows and noarch archives are zips
- **WHEN** `Release` or `release-windows-x64` completes
- **THEN** its archive in `.output/` has the `.zip` extension and is a valid zip file

#### Scenario: Linux and macOS archives are tarballs
- **WHEN** `release-linux-x64`, `release-linux-arm64` or `release-macos-arm64` completes
- **THEN** its archive in `.output/` has the `.tgz` extension and is a valid gzip-compressed tar
  file (readable by `tar xzf`)
- **AND** no `.zip` archive is produced by that target

#### Scenario: Archive layout is the same in both formats
- **WHEN** a release archive is extracted, in either format
- **THEN** the published files sit at the archive root, with no wrapping top-level directory

### Requirement: Tarball preserves the executable bit
In a `.tgz` release archive, the `lazynats` application binary SHALL carry Unix permissions that
include the owner execute bit, regardless of the OS of the host that built the archive, so the
user who extracts it can run it without a `chmod`.

#### Scenario: Built natively on Linux or macOS
- **WHEN** `release-linux-x64`, `release-linux-arm64` or `release-macos-arm64` builds natively on
  a matching host and the archive is extracted with `tar xzf`
- **THEN** the extracted `lazynats` binary can be executed directly, without a `chmod`

#### Scenario: Built via the Docker fallback on a Windows host
- **WHEN** `release-linux-x64` or `release-linux-arm64` takes the Docker fallback path on a Windows
  host and the archive is extracted on Linux with `tar xzf`
- **THEN** the extracted `lazynats` binary can be executed directly, without a `chmod`

### Requirement: Every release archive has a checksum sidecar
Each release target SHALL write a `<archive-file-name>.sha256` file next to its archive, in either
format, containing the lowercase hex SHA-256 of the archive followed by two spaces and the archive
file name.

#### Scenario: Checksum for a tarball
- **WHEN** `release-linux-x64` produces `lazynats-<version>-linux-x64.tgz`
- **THEN** `lazynats-<version>-linux-x64.tgz.sha256` exists alongside it and verifies with
  `sha256sum -c`

## MODIFIED Requirements

### Requirement: Framework-dependent Release build
The `Release` target SHALL publish `lazynats` as a framework-dependent, non-self-contained build
(no forced Native AOT), producing `lazynats-<version>-noarch.zip` in `.output/`.

#### Scenario: Release runs without a RID or AOT
- **WHEN** the `Release` target is executed
- **THEN** the published output is framework-dependent (requires a matching .NET runtime to run,
  is not self-contained, and is not a Native AOT binary)
- **AND** the zip artifact is named `lazynats-<version>-noarch.zip`

### Requirement: Linux x64 native release archive via Docker
A `release-linux-x64` target SHALL produce a self-contained, Native AOT `linux-x64` build of
`lazynats`, archived as `lazynats-<version>-linux-x64.tgz` in `.output/`. When the executing host's
OS and CPU architecture already match `linux-x64` (i.e. the host is natively Linux x64), the
target SHALL publish directly on the host without Docker. Otherwise it SHALL fall back to
publishing inside a Docker container, so a non-Linux or non-x64 host can still produce the
artifact.

#### Scenario: Building on a native Linux x64 host
- **WHEN** `release-linux-x64` is executed on a host whose OS is Linux and whose CPU architecture
  is x64
- **THEN** `lazynats` is published self-contained for `linux-x64` with Native AOT enabled directly
  on the host, without starting a Docker container
- **AND** the result is archived to `.output/lazynats-<version>-linux-x64.tgz`

#### Scenario: Building from a host that isn't native Linux x64
- **WHEN** `release-linux-x64` is executed on a host that is not both Linux and x64
- **THEN** a Linux build container (.NET SDK plus the Native AOT Linux prerequisites) publishes
  `lazynats` self-contained for `linux-x64` with Native AOT enabled
- **AND** the result is archived to `.output/lazynats-<version>-linux-x64.tgz` on the host

#### Scenario: Docker unavailable and native doesn't apply
- **WHEN** `release-linux-x64` is executed on a host that is not native Linux x64, and Docker is
  not running or not installed
- **THEN** the target fails outright with Docker's own error surfaced, rather than silently
  skipping or producing a partial artifact

### Requirement: Linux arm64 native release archive via emulated Docker
A `release-linux-arm64` target SHALL produce a self-contained, Native AOT `linux-arm64` build of
`lazynats`, archived as `lazynats-<version>-linux-arm64.tgz` in `.output/`. When the executing
host's OS and CPU architecture already match `linux-arm64` (i.e. the host is natively Linux
arm64), the target SHALL publish directly on the host without Docker. Otherwise it SHALL fall back
to publishing inside a Docker container targeting `linux/arm64`, using QEMU emulation if the Docker
engine's own platform doesn't natively match `linux/arm64` either - a fact the target does not
assert in advance, since Docker resolves it transparently.

#### Scenario: Building on a native Linux arm64 host
- **WHEN** `release-linux-arm64` is executed on a host whose OS is Linux and whose CPU
  architecture is arm64
- **THEN** `lazynats` is published self-contained for `linux-arm64` with Native AOT enabled
  directly on the host, without starting a Docker container
- **AND** the result is archived to `.output/lazynats-<version>-linux-arm64.tgz`

#### Scenario: Building from a host that isn't native Linux arm64
- **WHEN** `release-linux-arm64` is executed on a host that is not both Linux and arm64
- **THEN** a `linux/arm64` Docker build container (.NET SDK plus the Native AOT Linux
  prerequisites) publishes `lazynats` self-contained for `linux-arm64` with Native AOT enabled,
  with Docker transparently deciding whether QEMU emulation is required
- **AND** the result is archived to `.output/lazynats-<version>-linux-arm64.tgz` on the host

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
produce a self-contained, Native AOT `osx-arm64` build of `lazynats`, archived as
`lazynats-<version>-macos-arm64.tgz` in `.output/`. Everywhere else it SHALL fail immediately with
a message explaining that a macOS Native AOT build requires a macOS arm64 host, rather than being
silently absent or appearing to succeed.

#### Scenario: Building on a native macOS arm64 host
- **WHEN** `release-macos-arm64` is executed on a host whose OS is macOS and whose CPU
  architecture is arm64
- **THEN** `lazynats` is published self-contained for `osx-arm64` with Native AOT enabled directly
  on the host
- **AND** the result is archived to `.output/lazynats-<version>-macos-arm64.tgz`

#### Scenario: Invoking on a non-matching host
- **WHEN** `release-macos-arm64` is executed on a host that is not both macOS and arm64
- **THEN** it fails immediately with a message noting that a macOS Native AOT build requires a
  macOS arm64 host

### Requirement: Release archives exclude debug symbol files
`Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, and
`release-macos-arm64` SHALL each strip debug symbol files (`*.pdb` on Windows, `*.dbg` on Linux
and macOS) from the published output before archiving, so the resulting archive (`.zip` or
`.tgz`) does not carry them.

#### Scenario: Archiving a published output
- **WHEN** any of `Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` archives its published output
- **THEN** the resulting archive contains no `.pdb` or `.dbg` files

### Requirement: Re-running a release target overwrites its zip
`Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, and
`release-macos-arm64` SHALL each overwrite their destination archive (`.zip` or `.tgz`) and its
`.sha256` sidecar if they already exist from a previous run, rather than failing or appending to
them.

#### Scenario: Archive already exists from a prior run
- **WHEN** any of `Release`, `release-windows-x64`, `release-linux-x64`, `release-linux-arm64`, or
  `release-macos-arm64` is run again while its destination archive already exists in `.output/`
- **THEN** the target completes successfully and the archive is replaced with the new output
- **AND** the `.sha256` sidecar matches the new archive
