## 1. Framework-dependent `Release`

- [x] 1.1 Remove `<PublishAot>true</PublishAot>` from `src/lazynats/lazynats.csproj`.
- [x] 1.2 Confirm `dotnet run --project src/lazynats` still launches normally.
- [x] 1.3 Run the existing `Release` target and confirm `lazynats-<version>.zip` is still produced
      in `.output/` with the same name as before, now containing a framework-dependent publish
      (no RID-specific runtime assets, no native AOT binary).

## 2. Linux x64 Docker build image

- [x] 2.1 Add `docker/release-linux-x64.dockerfile`: FROM the `mcr.microsoft.com/dotnet/sdk`
      image matching the project's target framework, `apt-get install clang zlib1g-dev`.
- [x] 2.2 Confirm the image builds standalone (`docker build -f
      docker/release-linux-x64.dockerfile .`) before wiring it into Nuke.

## 3. `release-windows-x64` target

- [x] 3.1 Add a `release-windows-x64` Target in `.nuke/build/Program.cs`, depending on `Restore`.
- [x] 3.2 Guard execution with `OperatingSystem.IsWindows()`; throw a clear, descriptive exception
      immediately if false.
- [x] 3.3 Publish `lazynats` via `DotNetPublish` with `-r win-x64 --self-contained
      -p:PublishAot=true`, output to a dedicated subfolder under `.output/` (not the same path
      `Release` uses).
- [x] 3.4 Zip the published output to `.output/lazynats-<version>-windows-x64.zip` using the
      existing `CompressTo` extension.
- [x] 3.5 Run it locally on Windows and confirm the zip is produced and its contents run
      standalone (no separately-installed .NET runtime required).

## 4. `release-linux-x64` target

- [x] 4.1 Add a `release-linux-x64` Target in `.nuke/build/Program.cs`, depending on `Restore`.
- [x] 4.2 Build the `docker/release-linux-x64.dockerfile` image (mirroring the existing
      `DockerBuild` usage in `ReleaseDocker`).
- [x] 4.3 Run the image via `docker run`, bind-mounting the repo and a host-side output directory,
      executing `dotnet publish` for `src/lazynats` with `-r linux-x64 --self-contained
      -p:PublishAot=true` inside the container.
- [x] 4.4 Zip the host-side published output to `.output/lazynats-<version>-linux-x64.zip` using
      the existing `CompressTo` extension.
- [x] 4.5 Run it locally (Docker Desktop running) and confirm the zip is produced; if a Linux
      machine or WSL is available, confirm the binary actually runs there.

## 5. Placeholder targets

- [x] 5.1 Add a `release-linux-arm64` Target that immediately throws a descriptive exception,
      with a code comment explaining the two future implementation paths (QEMU-emulated
      `--platform linux/arm64` container, or a clang/binutils-aarch64/sysroot cross-toolchain).
- [x] 5.2 Add a `release-macos-arm64` Target that immediately throws a descriptive exception, with
      a code comment noting it requires an actual macOS build host.
- [x] 5.3 Confirm both show up in target discovery (e.g. `./build.ps1 --help` or `--plan`) and
      that invoking either fails fast with the intended message.

## 6. Sanity checks

- [x] 6.1 Confirm none of the four new targets are reachable via `.DependsOn`/`.Before`/`.After`
      from `Release`, `Build`, or `Rebuild` — each only runs when named explicitly.
- [x] 6.2 Confirm `Release`, `ReleaseDocker`, `PublishToNuget`, and `PublishToGitHub` still behave
      as before (unaffected by this change).
- [x] 6.3 Strip debug symbol files (`*.pdb`, `*.dbg`) from the published output before zipping, in
      `Release`, `release-windows-x64`, and `release-linux-x64` alike — verified each zip no
      longer contains them.
- [x] 6.4 Delete a pre-existing destination zip before compressing (in `Release`,
      `release-windows-x64`, `release-linux-x64` alike) so re-running a target doesn't fail with
      `IOException: file already exists` — verified by running `release-windows-x64` twice in a
      row.
