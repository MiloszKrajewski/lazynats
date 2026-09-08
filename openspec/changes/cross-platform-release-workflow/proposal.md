## Why

`release-windows-x64`, `release-linux-x64`, and `release-linux-arm64` exist as Nuke targets and
work locally, but the two Linux targets always cross-build through Docker (+QEMU for arm64) even
when unnecessary, `release-macos-arm64` is an unconditional placeholder that always fails, none of
the four targets are wired to a GitHub Actions workflow, and `PublishToGitHub` globs `*.nupkg` for
its release assets — always empty, since `lazynats` isn't a packable project — so nothing today
actually publishes a release zip anywhere. GitHub now hosts native arm64 Linux runners
(`ubuntu-24.04-arm`) and Apple Silicon macOS runners (`macos-latest`), so most of these builds can
run natively in CI without Docker or QEMU at all, and a real macOS arm64 build becomes possible
for the first time.

## What Changes

- Generalize the Linux release targets: `release-linux-x64`/`release-linux-arm64` build natively
  (plain `dotnet publish -r <rid> --self-contained -p:PublishAot=true`, no Docker) whenever the
  current host's OS+arch already matches the target RID, and fall back to the existing Docker
  path only when it doesn't. Docker's own `--platform` flag continues to decide transparently
  whether QEMU emulation is needed - the build never hardcodes that assumption.
- Extract the native-publish logic already used by `release-windows-x64` into a shared helper so
  Linux (when native applies) and the new macOS target reuse it instead of duplicating it.
- Implement `release-macos-arm64` for real: builds natively on a macOS arm64 host, and still fails
  immediately with a clear "wrong host" message everywhere else (e.g. from a Windows dev box) -
  no Docker fallback exists for macOS.
- Fix `PublishToGitHub` to upload the actual platform release zips
  (`lazynats-<version>-<system>-<arch>.zip`) instead of the always-empty `*.nupkg` glob.
- Add `.github/workflows/release.yml`, manually triggered (`workflow_dispatch`) only: one job per
  platform (`windows-latest`, `ubuntu-latest`, `ubuntu-24.04-arm`, `macos-latest`), each running
  its matching Nuke target on a host that's native for that target (so none of the CI jobs ever
  exercise the Docker fallback), uploading its zip as a build artifact; a final `publish` job
  downloads all four zips and runs `PublishToGitHub` to create one GitHub Release with all of them
  attached. The two Linux jobs provision `clang`/`zlib1g-dev` via a workflow step before invoking
  Nuke, since that's what native Linux AOT needs and the Docker image existed only to carry it
  onto non-Linux hosts.
- No workflow-side version parsing: `CHANGES.md` is still read only inside the Nuke build (as it
  is today), and zip/release-tag naming already carries the resolved version.

## Capabilities

### New Capabilities
- `release-publish-workflow`: the `workflow_dispatch` GitHub Actions pipeline that builds all four
  platform archives in parallel jobs on natively-matching runners and publishes them to a single
  GitHub Release, including the `PublishToGitHub` artifact-selection fix that makes that publish
  attach the right files.

### Modified Capabilities
- `platform-release-builds`: `release-linux-x64` and `release-linux-arm64` now build natively when
  the host already matches the target OS+arch, using Docker only as a fallback; `release-macos-arm64`
  is no longer an unconditional placeholder - it builds for real on a matching host and only fails
  elsewhere; the debug-symbol-stripping and overwrite-on-rerun requirements extend to cover the
  now-real macOS target.

## Impact

- `.nuke/build/Program.cs`: new shared native-publish helper; host-match branching added to
  `ReleaseLinuxX64`/`ReleaseLinuxArm64`; `ReleaseMacosArm64` implemented; `PublishToGitHub`'s
  artifact glob fixed.
- New `.github/workflows/release.yml`.
- `openspec/specs/platform-release-builds/spec.md`: requirement deltas for the two Linux targets
  and the macOS target.
- New `openspec/specs/release-publish-workflow/spec.md`.
