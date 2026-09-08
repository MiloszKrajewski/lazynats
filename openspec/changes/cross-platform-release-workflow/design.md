## Context

`.nuke/build/Program.cs` already has `release-windows-x64` (native, Windows-only),
`release-linux-x64`/`release-linux-arm64` (always via Docker, optionally QEMU-emulated), and
`release-macos-arm64` (unconditional placeholder that always fails) as independently-invokable
targets, plus a `PublishToGitHub` target that creates a GitHub Release via `GitHubApi.Release`
using the version resolved from `CHANGES.md`. None of this is wired to a GitHub Actions workflow;
the only workflow today (`continuous.yml`) runs the framework-dependent `Release` target on push
and uploads its output as a plain build artifact, not a release. `PublishToGitHub`'s artifact glob
(`*.{PackageVersion}.nupkg`) is also stale - `lazynats` isn't a packable project, so it's always
empty.

GitHub-hosted runners now include native arm64 Linux (`ubuntu-24.04-arm`) and Apple Silicon macOS
(`macos-latest`, since macOS 14), so a CI job can be native for every platform this project
targets, without Docker or QEMU anywhere in CI.

## Goals / Non-Goals

**Goals:**
- Let each platform target build natively whenever the executing host's OS+arch already matches
  the target RID, on any host (CI or local), falling back to Docker only when it doesn't.
- Make `release-macos-arm64` actually work on a matching host (CI's `macos-latest`) instead of
  always failing.
- Add one manually-triggered workflow that builds all four platforms in parallel, natively, and
  publishes a single GitHub Release with all four zips attached.
- Fix `PublishToGitHub` so it uploads the right files.

**Non-Goals:**
- No push-triggered/automatic releases - `release.yml` is `workflow_dispatch` only.
- No Windows-arm64 or Linux-arm32 targets - only the four platforms already named.
- No change to `continuous.yml`, `PublishToNuget`, or `ReleaseDocker`.
- No cross-building macOS from a non-macOS host - still impossible, still fails clearly.
- No workflow-side version parsing - `CHANGES.md` stays a Nuke-only concern, as it is today.

## Decisions

**Host-match native selection replaces environment-based gating.** Each Linux/macOS target
compares the executing host's OS and CPU architecture (`OperatingSystem.IsLinux()`/`IsMacOS()` +
`RuntimeInformation.OSArchitecture`) against its own target RID. Match -> publish natively on the
host. No match -> Docker (Linux targets only; macOS has no Docker fallback, same as Windows).
Rejected alternative: gating on `GITHUB_ACTIONS` - that conflates "are we in CI" with "does the
host match," which is the wrong axis: a native Linux dev box should also skip Docker, and if
GitHub ever changes what a given runner label maps to, an env-var gate wouldn't notice while a
capability check still would.

**Shared `PublishNative` helper.** `release-windows-x64` already does exactly the native-publish
sequence (`dotnet publish -r <rid> --self-contained -p:PublishAot=true`, strip symbols, zip).
Extract that into a helper parameterized by `(project, rid, archSuffix)`, reused by Windows
(unconditionally) and by Linux/macOS (when the host matches). `PublishLinuxViaDocker` is untouched
and keeps serving as the Linux fallback from a non-matching host.

**Docker's `--platform` is trusted to resolve QEMU itself.** Neither `release-linux-x64` nor
`release-linux-arm64` asserts whether emulation is in play - that was already implicit in the
existing `PublishLinuxViaDocker` (its `exec format error`/`no matching manifest` catch block only
makes sense if the code doesn't know in advance). The spec update keeps this framing: emulation is
a property of the *host running the fallback*, not of the target.

**apt-get provisioning lives in the workflow, not in Nuke.** `docker/release-linux-x64.dockerfile`
exists only to carry `clang`/`zlib1g-dev` onto non-Linux hosts. On a native Linux CI runner, that's
plain environment setup, not a build step - it belongs in `release.yml` as an `apt-get install`
step before the Nuke target runs, keeping the build script itself free of privileged system-package
concerns.

**One workflow, four build jobs + one publish job.** `windows-latest`, `ubuntu-latest`,
`ubuntu-24.04-arm`, and `macos-latest` each run their one matching Nuke target and
`actions/upload-artifact` the resulting zip. A `publish` job (`needs: [all four]`, plain
`ubuntu-latest`, no AOT/Docker involved) downloads all four zips and runs the fixed
`PublishToGitHub`, which still does its own draft-create -> upload -> publish, and still skips
cleanly if a release for that version already exists (unchanged `GitHubApi.Release` behavior).
Using `needs` on all four means a single platform failure blocks the release rather than
publishing a partial one.

**`PublishToGitHub`'s artifact selection changes from `*.nupkg` to the platform zips.** Since the
zips already carry `PackageVersion` in their filename (`lazynats-<version>-<system>-<arch>.zip`),
no additional version plumbing is needed between the workflow and the Nuke target - the
existing `CHANGES.md`-driven `PackageVersion` property continues to be the single source of truth.

## Risks / Trade-offs

- **`ubuntu-24.04-arm`/`macos-latest` runner minutes cost more than `ubuntu-latest`, and arm64
  Linux runners are metered on private repos** -> Mitigated by the workflow being manual-only, so
  cost is bounded to explicit release runs, not every push.
- **A single failed platform job would otherwise let the others "succeed" with no release
  published, silently** -> Mitigated by making `publish` depend on all four jobs via `needs`; a
  failed build job skips `publish` entirely rather than creating a partial release.
- **Host OS/arch detection could misclassify an unusual runner and silently pick the wrong path**
  -> Keep the match check narrow and explicit (exact OS+arch pairs the four targets already name);
  anything that doesn't match one of those known combinations falls to the existing Docker/failure
  path rather than guessing.
- **Re-running the workflow for a version that's already released** -> Unchanged existing
  behavior: `GitHubApi.Release` detects the existing release tag and skips (logs a warning),
  rather than duplicating or erroring - still true with zips instead of nupkgs as the payload.

## Migration Plan

1. Refactor `.nuke/build/Program.cs`: extract `PublishNative`, add host-match branching to
   `ReleaseLinuxX64`/`ReleaseLinuxArm64`, implement `ReleaseMacosArm64` for a matching host, fix
   `PublishToGitHub`'s glob. Verify locally on the Windows dev box: `release-windows-x64` behaves
   as before, `release-linux-x64`/`release-linux-arm64` still take the Docker path (host doesn't
   match), `release-macos-arm64` still fails locally with a clear message.
2. Add `.github/workflows/release.yml`. No changes to `continuous.yml`.
3. Do a first manual `workflow_dispatch` run and confirm all four jobs go native (no Docker log
   output in any of them) and the resulting GitHub Release carries all four zips.
4. Rollback, if needed: revert the workflow file and Nuke changes; no persistent state beyond any
   GitHub Release created by a run, which can be deleted manually like any other release.

## Open Questions

- Repo visibility (public vs. private) determines whether `ubuntu-24.04-arm`/`macos-latest`
  minutes are free or billed - doesn't affect the design, just the operational cost of using it.
