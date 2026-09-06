## Why

`lazynats.csproj` hardcodes `<PublishAot>true</PublishAot>`, so the existing Nuke `Release`
target's `dotnet publish` (no `-r`, no explicit self-contained/AOT flags) silently attempts a
native-AOT build using whatever RID the SDK infers for the build machine — it was never actually
producing the portable, framework-dependent artifact it looks like it produces. There is also no
way today to produce real, intentional native-AOT release artifacts for a specific OS/arch
(Windows x64, Linux x64 via Docker so it doesn't require a Linux host) with predictable,
discoverable zip names. Both gaps need closing before release artifacts can be trusted or
distributed.

## What Changes

- Remove `<PublishAot>true</PublishAot>` from `src/lazynats/lazynats.csproj`. `dotnet run` /
  `dotnet build` are unaffected (the property only ever applied to `dotnet publish`); the
  existing `Release` target's publish step becomes what it always looked like it was — a plain
  framework-dependent, non-self-contained publish. Its output zip name
  (`lazynats-<version>.zip`) is unchanged. `src/lazynats.AotProbe` keeps its own
  `PublishAot=true` untouched — it's a separate AOT/trim validation harness, not part of the
  release pipeline.
- Add a `release-windows-x64` Nuke target (Windows hosts only): `dotnet publish -r win-x64
  --self-contained -p:PublishAot=true`, zipped to
  `.output/lazynats-<version>-windows-x64.zip`.
- Add a `release-linux-x64` Nuke target (any host, via a new Docker image containing the .NET SDK
  and the Native AOT Linux build prerequisites — `clang`, `zlib1g-dev`): `dotnet publish -r
  linux-x64 --self-contained -p:PublishAot=true` run inside that container, zipped to
  `.output/lazynats-<version>-linux-x64.zip`.
- Add `release-linux-arm64` and `release-macos-arm64` Nuke targets as explicit placeholders that
  fail immediately with a message explaining why (no QEMU/cross-compile toolchain wired up yet;
  no macOS build host available) and what the future implementation path looks like. They exist
  now so the full target surface is discoverable, without pretending they work.
- All four new targets are invoked explicitly (e.g. `./build.ps1 release-windows-x64`); none of
  them are added as a dependency of the existing `Release` target, and `Release` gains no new
  dependents.

## Capabilities

### New Capabilities
- `platform-release-builds`: Nuke targets that produce OS/arch-specific native-AOT release
  archives (today: Windows x64, Linux x64 via Docker) plus explicit placeholder targets for the
  not-yet-supported combinations (Linux arm64, macOS arm64), each following the
  `lazynats-<version>-<system>-<arch>.zip` naming convention in `.output/`.

### Modified Capabilities
(none — no existing `openspec/specs/` capability covers build/release tooling)

## Impact

- `src/lazynats/lazynats.csproj`: drop `PublishAot`.
- `.nuke/build/Program.cs`: new targets `release-windows-x64`, `release-linux-x64`,
  `release-linux-arm64`, `release-macos-arm64`.
- New Docker asset under `docker/` for the Linux x64 AOT build environment (separate from
  `docker/default.dockerfile`, which packages the *app* into a runtime image, not the *build*
  environment).
- No change to `Release`, `ReleaseDocker`, `PublishToNuget`, `PublishToGitHub`, or the
  `continuous` GitHub Actions workflow.
