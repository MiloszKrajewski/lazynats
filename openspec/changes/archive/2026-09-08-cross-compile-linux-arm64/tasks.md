## 1. Docker build assets

- [x] 1.1 Confirm `docker/release-linux-x64.dockerfile` builds and installs `clang`/`zlib1g-dev`
      correctly under `--platform linux/arm64` (base image `mcr.microsoft.com/dotnet/sdk:10.0`
      publishes arm64 variants); rename to `docker/release-linux.dockerfile` only if reuse across
      both arch targets under the `-x64` name reads confusingly, updating both targets' file
      references if renamed.

## 2. Build script changes

- [x] 2.1 Verify `SetPlatform` is available on the Docker settings types
      (`Nuke.Common.Tools.Docker`, v10.1.0) for both the `DockerBuild` and `DockerRun` fluent
      calls; fall back to raw CLI arguments only if it isn't.
- [x] 2.2 Extract a shared private helper in `.nuke/build/Program.cs` (e.g.
      `PublishLinuxViaDocker(Project project, string rid, string platform, string archSuffix)`)
      that builds the builder image tagged `lazynats-release-linux-<archSuffix>-builder` with the
      given `--platform`, runs `dotnet publish --runtime <rid> --self-contained
      -p:PublishAot=true` inside it with the same volume/workdir wiring as today's
      `ReleaseLinuxX64`, strips debug symbols, and zips to
      `lazynats-<version>-linux-<archSuffix>.zip`.
- [x] 2.3 Reimplement `ReleaseLinuxX64` to call the shared helper with `rid: "linux-x64"`,
      `platform: "linux/amd64"`, `archSuffix: "x64"`, preserving its current observable behavior
      (same image tag, same zip name, same `Restore` dependency).
- [x] 2.4 Reimplement `ReleaseLinuxArm64` to call the shared helper with `rid: "linux-arm64"`,
      `platform: "linux/arm64"`, `archSuffix: "arm64"`, depending on `Restore` like the other
      platform targets, and remove the `NotSupportedException` placeholder body.
- [x] 2.5 Add `.After(Release)` (not `.DependsOn(Release)`) to all four platform targets —
      `ReleaseWindowsX64`, `ReleaseLinuxX64`, `ReleaseLinuxArm64`, and `ReleaseMacosArm64` —
      alongside their existing `.DependsOn(Restore)`/placeholder bodies, so `Release` orders
      before them when both are scheduled in one invocation but is never triggered by invoking a
      platform target alone.
- [x] 2.6 Wrap the Docker build/run calls (in the shared helper) so that a detected
      exec-format/emulation failure (e.g. stderr matching `exec format error` or `no matching
      manifest for linux/arm64`) is rethrown with a message explaining that `linux/arm64`
      emulation must be registered (e.g. via `docker run --privileged --rm tonistiigi/binfmt
      --install arm64`, or by using Docker Desktop, which registers it automatically), while
      other Docker failures (Docker not running, image build failure) continue to surface
      Docker's own error unchanged.

## 3. Verification

- [x] 3.1 Run `./build.ps1 release-linux-arm64` on a host with Docker + arm64 emulation available
      and confirm `.output/lazynats-<version>-linux-arm64.zip` is produced, contains no `.pdb`/
      `.dbg` files, and its binary is an ELF arm64 executable (e.g. via `file` inside a throwaway
      arm64 container, or `readelf -h`).
- [x] 3.2 Re-run `./build.ps1 release-linux-arm64` a second time and confirm the zip is
      overwritten without error.
- [x] 3.3 Re-run `./build.ps1 release-linux-x64` and confirm it still behaves exactly as before
      (same image tag, same zip contents/name) after the shared-helper refactor.
- [x] 3.4 Run `./build.ps1 Release` and confirm neither `release-linux-x64` nor
      `release-linux-arm64` execute as a side effect.
- [x] 3.5 Run `./build.ps1 release-windows-x64` (or another platform target) on its own and
      confirm `Release` is not triggered; then run `./build.ps1 Release release-linux-x64` in one
      invocation and confirm `Release` executes before `release-linux-x64`.

## 4. Documentation and spec sync

- [x] 4.1 Remove the `cross complation for linux-arm64` line from `TODO.md`.
- [x] 4.2 Confirm the delta spec under
      `openspec/changes/cross-compile-linux-arm64/specs/platform-release-builds/spec.md` matches
      the implemented behavior before archiving (adjust wording if the QEMU-failure detection
      ends up keying off different error text than drafted).
