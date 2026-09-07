## Context

`release-linux-x64` (`.nuke/build/Program.cs`) already proves out the "Docker as a Linux Native
AOT build environment" pattern: it builds a builder image from `docker/release-linux-x64.dockerfile`
(the .NET SDK image plus `clang`/`zlib1g-dev`, the Native AOT prerequisites on Linux), then runs
`dotnet publish --runtime linux-x64 --self-contained -p:PublishAot=true` inside a container of
that image, with the repo and an output directory bind-mounted in. This works without a Linux
host because Docker Desktop/Engine on Windows and macOS run Linux containers natively for the
host's own architecture.

`release-linux-arm64` is currently a placeholder (`.nuke/build/Program.cs:268-279`) that always
throws, with a comment naming two possible future implementations: (1) a QEMU-emulated
`linux/arm64` container mirroring `release-linux-x64`, or (2) a native `clang`/`binutils-aarch64`/
sysroot cross-toolchain. The `Nuke.Common.Tools.Docker` wrapper (`Nuke.Common` 10.1.0) exposes
`SetPlatform` on its Docker settings types, so `docker build --platform` / `docker run --platform`
are reachable through the existing fluent API without dropping to raw arguments.

## Goals / Non-Goals

**Goals:**
- Make `release-linux-arm64` produce a working self-contained Native AOT `linux-arm64` build,
  runnable from any host that has Docker (matching `release-linux-x64`'s "no Linux host needed"
  property).
- Reuse as much of `release-linux-x64`'s structure and Docker asset as practical, rather than
  inventing a parallel, drifting implementation.
- Fail loudly and specifically when the host can't run emulated `linux/arm64` containers (missing
  QEMU/binfmt registration), rather than surfacing Docker's generic, hard-to-diagnose error.

**Non-Goals:**
- Native (non-emulated) cross-compilation via a clang/binutils-aarch64/sysroot toolchain
  (option 2 from the placeholder comment). It would be faster to build but adds a toolchain to
  provision and maintain on every build host; QEMU emulation reuses infrastructure this project
  already depends on (Docker) and keeps `release-linux-arm64` structurally identical to
  `release-linux-x64`. Can be revisited later if emulated build times become a real problem.
- Wiring `release-linux-arm64` into CI, or making `Release` (or any other target) trigger it
  automatically. The only ordering added is `.After(Release)` (see Decisions) — a hint for when
  both are scheduled together, not a dependency.
- Speeding up QEMU emulation (e.g. binfmt tuning, build caching) beyond what falls out naturally.

## Decisions

- **QEMU-emulated Docker container, not a cross-toolchain.** Chosen over the cross-toolchain
  option for parity with `release-linux-x64` and because it needs no new host-level toolchain
  provisioning — only Docker, which the project already requires for `release-linux-x64`. Trade-off
  is emulated-QEMU build time (likely several times slower than native), accepted as a Non-Goal
  above.

- **Reuse `docker/release-linux-x64.dockerfile` unmodified rather than adding a second dockerfile.**
  The image only needs `clang` + `zlib1g-dev` on top of the .NET SDK base image; that requirement
  doesn't differ by target *runtime* arch, only by the *container's own* platform, which
  `docker build --platform linux/arm64` already controls. Building it under `--platform
  linux/arm64` pulls the arm64 SDK base image and installs arm64 `clang`/`zlib1g-dev` inside an
  emulated arm64 container — giving a container whose `dotnet`/`clang` genuinely run as arm64
  under emulation, which is what `PublishAot` needs (its native compile/link step runs inside the
  container, not cross-compiled from x64). This avoids maintaining a near-duplicate dockerfile.
  Rename it to `docker/release-linux.dockerfile` only if reuse across both targets reads
  confusingly under the current name; otherwise leave the filename as-is and pass a tag alone.

- **Extract a shared helper for the two Docker-based publish targets.** `ReleaseLinuxX64` and the
  new `ReleaseLinuxArm64` differ only in: image tag, `docker build`/`docker run` `--platform`
  value, the `--runtime` RID passed to `dotnet publish`, and the output zip's arch suffix. Extract
  a private helper (e.g. `PublishLinuxViaDocker(Project project, string rid, string platform)`) in
  `Program.cs` called by both targets, rather than duplicating the ~25-line block. This mirrors
  the existing `RemoveDebugSymbols`/`CompressToFresh` shared-helper style already used across all
  three platform targets.

- **Platform selection via `SetPlatform("linux/arm64")` on both `DockerBuild` and `DockerRun`.**
  Both the builder-image build and the publish run must be emulated arm64 — the image built for
  `linux/amd64` (the implicit default on an x64 host) cannot run arm64 binaries, and `dotnet
  publish --runtime linux-arm64 -p:PublishAot=true` needs to invoke an arm64 `clang`/linker, which
  only exists inside an arm64 container.

- **Detect missing QEMU/binfmt support and fail with a specific message, not Docker's raw error.**
  Wrap the `DockerRun` (or the whole helper) in a `try/catch` that recognizes the
  exec-format-error signature Docker surfaces when `binfmt_misc` isn't registered for the target
  architecture (e.g. stderr containing `exec format error` or `no match for platform in manifest`,
  the actual BuildKit wording — verified against a real Docker Desktop failure; older/classic
  Docker engines may instead say `no matching manifest for linux/arm64`, which is also matched)
  and rethrow
  with guidance ("install/enable QEMU user-mode emulation for Docker, e.g. `docker run --privileged
  --rm tonistiigi/binfmt --install arm64`, or use Docker Desktop which registers it automatically").
  Falling through to Docker's native error is acceptable for *other* failure modes (bad Dockerfile,
  network failure pulling the base image, etc.) — only the emulation-specific case needs a
  purpose-written message, matching the existing `release-linux-x64` behavior of letting Docker's
  own error surface for "Docker unavailable".

- **Image tag stays per-target (`lazynats-release-linux-arm64-builder`), not shared with x64.**
  Even though they'd build from the same dockerfile, the two images are platform-distinct
  (`linux/amd64` vs `linux/arm64`); giving them separate tags avoids one target's cached image
  silently being the wrong platform for the other, and matches `release-linux-x64`'s existing
  naming (`lazynats-release-linux-x64-builder`).

- **All four platform targets get `.After(Release)`, not `.DependsOn(Release)`.** `Release`
  cleans/rebuilds the whole solution and produces the nupkg; the platform archives are an
  independent, heavier follow-on someone may only want occasionally. `.DependsOn(Release)` would
  force `Release` to run every time any platform target is invoked, contradicting the existing
  "independently invoked" requirement. `.After(Release)` instead only affects *scheduling order
  when both happen to be requested together* (e.g. `./build.ps1 Release release-linux-arm64` in
  one invocation) — Nuke runs `Release` first in that case, but invoking `release-linux-arm64`
  alone still runs only `Restore` + itself. This is the same relationship `PublishToGitHub`
  already has with these targets (`.After(Release).After(ReleaseWindowsX64)...`), just applied one
  level earlier. Since none of `ReleaseWindowsX64`/`ReleaseLinuxX64` currently declare this
  ordering either, add `.After(Release)` to all four platform targets (`ReleaseWindowsX64`,
  `ReleaseLinuxX64`, the new `ReleaseLinuxArm64`, and the `ReleaseMacosArm64` placeholder) for
  consistency, not just the new arm64 one.

## Risks / Trade-offs

- **QEMU emulation is slow** → Accepted (Non-Goal); the target is for occasional release builds,
  not inner-loop iteration. If it becomes painful, the cross-toolchain option remains available as
  a follow-up change.
- **Docker Desktop vs. Linux Docker Engine differ in whether QEMU/binfmt is pre-registered**
  (Desktop bundles it; a bare Linux Engine install typically doesn't) → Mitigated by the
  specific error message pointing at `tonistiigi/binfmt` installation; documented as a prerequisite
  in the spec delta rather than auto-installed by the build (auto-installing would need
  `--privileged`, which this pipeline shouldn't grant itself silently).
- **Exec-format-error string matching is inherently a bit brittle** (Docker's exact wording could
  change across versions) → Acceptable: worst case the match misses and the raw Docker error
  surfaces, i.e. no worse than today's `release-linux-x64` behavior; this is a UX improvement, not
  a correctness requirement.

## Migration Plan

No migration — this only changes build tooling behavior for a target that currently always
throws. No rollback beyond reverting the commit; no runtime/user-facing state involved.

## Open Questions

- None outstanding; the shared-helper extraction and exact exception-matching heuristic are
  implementation details to finalize during `tasks`/apply.
