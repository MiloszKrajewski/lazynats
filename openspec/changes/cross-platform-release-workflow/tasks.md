## 1. Nuke build: shared native publish + host matching

- [x] 1.1 Add a host OS/arch match helper (e.g. comparing `OperatingSystem.IsLinux()`/
  `IsMacOS()`/`IsWindows()` plus `RuntimeInformation.OSArchitecture` against a target RID's OS+arch)
- [x] 1.2 Extract `PublishNative(project, rid, archSuffix)` from `ReleaseWindowsX64`'s existing
  body (publish, strip symbols, zip) so it's reusable
- [x] 1.3 Update `ReleaseWindowsX64` to call the shared helper (behavior unchanged)
- [x] 1.4 Update `ReleaseLinuxX64`: call `PublishNative` when the host matches `linux-x64`,
  otherwise keep calling the existing `PublishLinuxViaDocker`
- [x] 1.5 Update `ReleaseLinuxArm64`: call `PublishNative` when the host matches `linux-arm64`,
  otherwise keep calling the existing `PublishLinuxViaDocker`
- [x] 1.6 Implement `ReleaseMacosArm64`: call `PublishNative` when the host matches
  `osx-arm64`/macOS arm64, otherwise keep the existing immediate failure with its "requires a
  macOS arm64 host" message

## 2. Fix PublishToGitHub's release assets

- [x] 2.1 Add a glob/property for the platform release zips (`lazynats-<version>-*.zip` in
  `OutputDirectory`), alongside the existing `.nupkg`-based `PackageArtifactsPattern`
- [x] 2.2 Update `PublishToGitHub` to select the platform zips as the assets it uploads
- [x] 2.3 Update `VerifyArtifacts` (or add an equivalent check) so it verifies the platform zips
  exist before `PublishToGitHub` runs, consistent with the fixed artifact selection

## 3. Local verification of the Nuke changes

- [x] 3.1 Run `release-windows-x64` on the Windows dev box; confirm output and zip name are
  unchanged from before the refactor
- [x] 3.2 Run `release-linux-x64` and `release-linux-arm64` on the Windows dev box; confirm both
  still take the Docker fallback path (host doesn't match) and still produce correct zips
- [x] 3.3 Run `release-macos-arm64` on the Windows dev box; confirm it still fails immediately
  with the "requires a macOS arm64 host" message

## 4. GitHub Actions release workflow

- [x] 4.1 Create `.github/workflows/release.yml` with `workflow_dispatch` as its only trigger
- [x] 4.2 Add the `windows-x64` job (`windows-latest`): checkout, cache, run `release-windows-x64`,
  `actions/upload-artifact` the resulting zip
- [x] 4.3 Add the `linux-x64` job (`ubuntu-latest`): checkout, cache, `apt-get install -y clang
  zlib1g-dev`, run `release-linux-x64`, `actions/upload-artifact` the resulting zip
- [x] 4.4 Add the `linux-arm64` job (`ubuntu-24.04-arm`): checkout, cache, `apt-get install -y
  clang zlib1g-dev`, run `release-linux-arm64`, `actions/upload-artifact` the resulting zip
- [x] 4.5 Add the `macos-arm64` job (`macos-latest`): checkout, cache, run `release-macos-arm64`,
  `actions/upload-artifact` the resulting zip
- [x] 4.6 Add the `publish` job (`ubuntu-latest`, `needs: [windows-x64, linux-x64, linux-arm64,
  macos-arm64]`): checkout, `actions/download-artifact` all four zips into the output directory,
  run `PublishToGitHub` with the GitHub token secret

## 5. End-to-end verification

- [ ] 5.1 Manually dispatch `release.yml` and confirm all four build jobs complete with no Docker
  activity in their logs
- [ ] 5.2 Confirm the `publish` job creates one GitHub Release for the current `CHANGES.md`
  version with all four platform zips attached as assets
- [ ] 5.3 Re-dispatch the workflow without bumping `CHANGES.md` and confirm the `publish` job
  completes successfully while `PublishToGitHub` skips creating a duplicate release
