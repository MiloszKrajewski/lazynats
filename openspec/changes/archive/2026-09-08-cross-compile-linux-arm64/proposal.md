## Why

`release-linux-arm64` is currently a placeholder that always throws `NotSupportedException` (see
`.nuke/build/Program.cs`), so there is no way to produce a Native AOT `linux-arm64` release
artifact for `lazynats` even though `release-windows-x64` and `release-linux-x64` already work.
arm64 Linux (Raspberry Pi, AWS Graviton, other SBCs) is a realistic deployment target for a
terminal NATS client, and the `platform-release-builds` spec already documents this gap as a
known placeholder to fill in.

## What Changes

- Implement `release-linux-arm64` for real: publish a self-contained, Native AOT `linux-arm64`
  build of `lazynats`, zipped as `lazynats-<version>-linux-arm64.zip` in `.output/`, following
  the same `RemoveDebugSymbols` / overwrite-on-rerun / independently-invoked conventions as
  `release-windows-x64` and `release-linux-x64`.
- Build it via a QEMU-emulated `linux/arm64` Docker container, mirroring the existing
  `release-linux-x64` target's Docker-based approach (new `docker/release-linux-arm64.dockerfile`
  or a parameterized reuse of the x64 one), rather than a native cross-toolchain — this keeps the
  same "works from any host with Docker" property `release-linux-x64` already has, at the cost of
  slower (emulated) builds.
- Fail with a clear, actionable error message if the host's Docker cannot run `linux/arm64`
  containers (QEMU/binfmt not registered), instead of an opaque Docker error.
- Add `.After(Release)` ordering (not `.DependsOn(Release)`) to all four platform-specific release
  targets (`release-windows-x64`, `release-linux-x64`, `release-linux-arm64`,
  `release-macos-arm64`), so that `Release` runs first when both are scheduled in the same
  invocation, without forcing `Release` to run whenever a platform target is invoked alone.
- Update `openspec/specs/platform-release-builds/spec.md` (via a delta spec in this change) to
  describe `release-linux-arm64` as implemented and drop it from the "explicit placeholder"
  requirement, which will then cover only `release-macos-arm64`.
- Remove the corresponding line from `TODO.md`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `platform-release-builds`: `release-linux-arm64` becomes a real, working target (self-contained
  Native AOT `linux-arm64` build via an emulated Docker container) instead of a permanent
  placeholder; the "explicit placeholders for unsupported platforms" requirement narrows to
  `release-macos-arm64` only, and debug-symbol-stripping / overwrite-on-rerun requirements extend
  to cover `release-linux-arm64` alongside `release-windows-x64` and `release-linux-x64`. The
  "independently invoked" requirement gains an explicit ordering clause: all four platform targets
  run after `Release` when both are scheduled together, via `.After()` rather than `.DependsOn()`.

## Impact

- `.nuke/build/Program.cs`: replace the `ReleaseLinuxArm64` target body; likely extract a shared
  helper between it and `ReleaseLinuxX64` given how similar the two Docker-based publish flows
  are.
- `docker/`: new dockerfile (or reuse of `release-linux-x64.dockerfile` parameterized by target
  arch) for the arm64 build container.
- `openspec/specs/platform-release-builds/spec.md`: requirement text updates.
- `TODO.md`: remove the now-completed item.
- No changes to application code (`src/lazynats`) — this is build/release tooling only.
