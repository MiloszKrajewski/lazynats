## Context

`lazynats.csproj` hardcodes `<PublishAot>true</PublishAot>`. The existing Nuke `Release` target
publishes each application project with no `-r` and no explicit `SelfContained`/`PublishAot`
flags (`Program.cs:155-165`), so it currently inherits AOT-with-an-inferred-RID from the csproj
rather than doing the plain framework-dependent publish it looks like it's doing. There is no
existing `openspec/specs/` capability for build/release tooling — this is a build-pipeline change,
not an app-behavior change, but it still benefits from being scoped and reviewed the same way.

Per the existing `test-aot.ps1` (`src/lazynats.AotProbe`), Native AOT publishing is already
proven to work for `win-x64` locally. Native AOT for Linux requires `clang` on the build machine;
`osx-*` requires Apple's toolchain and can only be produced on macOS hardware — there is no
cross-linker for it from Windows or Linux (unlike Linux arm64, which is reachable from an x64 host
via either QEMU emulation or a portable clang/binutils-aarch64/sysroot cross-toolchain — neither is
being wired up yet).

## Goals / Non-Goals

**Goals:**
- Make the existing `Release` target's publish step be, in fact, the framework-dependent build it
  was always intended to look like.
- Add two real, working release targets: `release-windows-x64` (native, Windows hosts only) and
  `release-linux-x64` (via Docker, so it works from any host without requiring a Linux machine).
- Add two placeholder targets — `release-linux-arm64`, `release-macos-arm64` — that fail
  immediately and explain why, so the full intended target surface is discoverable via
  `./build.ps1 --help` / target listing without pretending unimplemented work is done.
- Keep each target's implementation self-contained and literal (its own RID string, its own
  output path, its own zip name) rather than introducing a shared RID/arch matrix abstraction —
  there are only three real targets right now, not enough repetition to justify one.

**Non-Goals:**
- No QEMU-emulated or cross-toolchain Linux arm64 build (deferred; placeholder only).
- No macOS build support of any kind (impossible without macOS hardware in this pipeline).
- No `win-arm64` or `osx-x64` targets — not requested, not scoped.
- No change to how `Release`, `ReleaseDocker`, `PublishToNuget`, `PublishToGitHub`, or the
  `continuous` GitHub Actions workflow are wired together. The new targets are not depended on by
  anything, and depend on nothing beyond `Restore`.
- No shared helper/abstraction unifying the publish+zip steps across targets.

## Decisions

**Remove `PublishAot` from the csproj rather than overriding it per-target.**
Keeping it `true` in the csproj and overriding to `false` for the framework-dependent case would
mean the *common* case (`dotnet run`, `dotnet build`, the existing `Release` publish) carries a
property it doesn't want, while every AOT target has to remember to re-assert something already
"on". Flipping the default (property removed; each AOT target passes `-p:PublishAot=true`
explicitly) means the flag lives next to the RID/self-contained flags it's coupled with anyway,
on the three targets that actually need it. `lazynats.AotProbe.csproj` is untouched — it's a
standalone validation harness outside `lazynats.sln`, not part of the release pipeline.

**No `release-noarch` target.** With `PublishAot` no longer forced on, the existing `Release`
target's publish step already produces a framework-dependent artifact — a dedicated target would
duplicate that `DotNetPublish` call for no behavioral difference. `Release` keeps its current zip
name (`lazynats-<version>.zip`); the new `<system>-<arch>` naming convention only applies to the
new OS/arch-specific targets.

**Host-gate `release-windows-x64` with `OperatingSystem.IsWindows()`, fail fast otherwise.**
Windows AOT, like macOS AOT, has no cross-compilation story from another OS — so there's nothing
useful to attempt on a non-Windows host. The target checks and throws immediately with a clear
message rather than letting `dotnet publish -r win-x64 --self-contained -p:PublishAot=true` fail
deep inside the SDK with a less obvious error.

**`release-linux-x64` builds inside a dedicated Docker image, not the existing
`docker/default.dockerfile`.** That file packages the *already-published app* into a runtime
image (`FROM mcr.microsoft.com/dotnet/aspnet:...`, `COPY` prebuilt output) — a different concern
from a *build environment* that needs the full SDK plus `clang`/`zlib1g-dev` to run `dotnet
publish ... -p:PublishAot=true` for Linux. `default.dockerfile` is a stale artifact (wrong base
image/version for this project) that's out of scope here and being fixed separately. A new file,
e.g.
`docker/release-linux-x64.dockerfile`, built once per release run (`docker build`, mirroring
`ReleaseDocker`'s existing `DockerBuild` usage) FROM the matching `mcr.microsoft.com/dotnet/sdk`
image with `clang`/`zlib1g-dev` layered on. The actual publish then runs via `docker run` with the
repo bind-mounted in, writing output to a host-side path so the same `CompressTo` extension
already used elsewhere zips it — no new zip-inside-container logic.

This is **not** cross-compilation: an x64 host running an x64 Linux container produces a native
`linux-x64` binary. It only starts approaching cross-compilation infrastructure once `arm64`
support is added later (which is exactly why that stays a placeholder for now instead of being
half-built).

**Distinct output subfolders per target.** `Directory.Build.props` sets
`AppendRuntimeIdentifierToOutputPath=false`, and existing targets already work around that by
passing an explicit `.SetOutput(...)` path per publish. The new targets follow the same pattern
with their own subfolder names (e.g. `OutputDirectory / "lazynats-win-x64"`,
`OutputDirectory / "lazynats-linux-x64"`) so they can't collide with each other, with `Release`'s
own output, or with a stale directory from a previous run in the same `.output/`.

**Placeholder targets throw, they don't silently no-op.** `release-linux-arm64` and
`release-macos-arm64` exist as real `Target` definitions (so they show up in target discovery)
whose `.Executes(...)` immediately throws a descriptive exception — never a
quiet-success/no-op — with an adjacent code comment naming the two follow-up options already
discussed (QEMU-emulated `--platform linux/arm64` container vs. a portable cross-toolchain, for
arm64; "requires an actual macOS host" for macOS). This keeps `./build.ps1
release-linux-arm64` from ever being mistaken for a working, if-empty, release.

## Risks / Trade-offs

- **Docker Desktop must be running for `release-linux-x64`.** → Accepted: this target has no
  fallback path (per the "assume Docker is available" scoping from the original ask); if Docker
  isn't running, it fails outright and visibly, which is the desired behavior rather than a
  hidden gap.
- **The new Dockerfile pins a base SDK image + `clang`/`zlib1g-dev`, another Dockerfile to keep in
  sync with .NET version bumps.** → Same maintenance shape as the existing `docker/*.dockerfile`
  files already in the repo; no new category of upkeep.
- **Removing `PublishAot` from the csproj changes the (previously accidental) behavior of the
  existing `Release` target's publish step.** → This is the intended fix, not a side effect, but
  it's worth calling out explicitly: anyone who was unknowingly relying on `Release`'s output
  being a native-AOT binary for their own machine's RID gets a framework-dependent build instead
  after this change. No such reliance is known to exist.
- **Four independent targets, no shared RID matrix.** → Deliberate per-target literal strings for
  now (see Non-Goals); if `win-arm64`/`osx-x64`/real `linux-arm64` support shows up later, revisit
  whether a shared helper earns its keep at that point.

## Migration Plan

1. Remove `<PublishAot>true</PublishAot>` from `src/lazynats/lazynats.csproj`.
2. Confirm `dotnet run --project src/lazynats` and the existing `Release` target still work
   (`Release`'s publish step becomes framework-dependent; its zip name is unchanged).
3. Add `docker/release-linux-x64.dockerfile`.
4. Add the four new `Target`s to `.nuke/build/Program.cs`.
5. Manually invoke `release-windows-x64` and `release-linux-x64` locally and confirm each
   produces the expected `.output/lazynats-<version>-<system>-x64.zip`.
6. Manually invoke `release-linux-arm64` and `release-macos-arm64` and confirm each fails with
   its explanatory message (this is the expected, correct outcome — not a bug to fix).

No rollback concerns beyond reverting the commit — nothing here touches runtime app behavior,
persisted data, or CI triggers.

## Open Questions

None blocking. Deferred by explicit scope decision, to revisit only if/when they're actually
needed: Linux arm64 (QEMU vs. cross-toolchain), macOS build support, `win-arm64`/`osx-x64`
targets, and whether `Release`/CI should eventually depend on the new targets.
