## 1. Archive helper

- [x] 1.1 Add `enum ArchiveFormat { Zip, Tgz }` and its extension lookup (`.zip` / `.tgz`) in
      `.nuke/build/`
- [x] 1.2 Change `CompressToFresh` to take the output directory, base name and `ArchiveFormat`,
      build the final file name itself, delete any existing archive, call Nuke's `CompressTo`
      (unchanged), then `WriteSha256File` (unchanged); return the archive path for logging

## 2. Call sites and globs

- [x] 2.1 Noarch `Release`: archive as `Zip` with base name `<name>-<version>-noarch`
- [x] 2.2 `PublishNative`: take an `ArchiveFormat` argument; `ReleaseWindowsX64` passes `Zip`,
      `ReleaseLinuxX64` / `ReleaseLinuxArm64` / `ReleaseMacosArm64` pass `Tgz` (wrap in a
      domain-named helper if the platform-to-format choice repeats)
- [x] 2.3 `PublishLinuxViaDocker`: archive as `Tgz`
- [x] 2.4 Replace `PlatformArtifactsPattern` with a lookup covering both `*-{version}-*.zip` and
      `*-{version}-*.tgz`; update `VerifyPlatformArtifacts` and `PublishToGitHub` to use it
- [x] 2.5 Rename local `zipName` variables / log messages to archive-neutral wording

## 3. Verification

- [x] 3.1 `./build.ps1 release` on Windows: `.output/lazynats-<version>-noarch.zip` + `.sha256`
      produced, extracts flat, no `.pdb`
- [x] 3.2 `./build.ps1 release-windows-x64`: `.zip` + `.sha256` produced, runs after extraction
- [x] 3.3 `./build.ps1 release-linux-x64` on Windows (Docker fallback): `.tgz` + `.sha256`
      produced; `tar -tzvf` shows `/`-separated flat entries, no `.dbg`, and `lazynats` with the
      owner execute bit; extracting in a Linux container and running `./lazynats --help` (or
      equivalent) works without `chmod`
- [x] 3.4 Re-run 3.3 with the archive already present: it's overwritten and `sha256sum -c` passes
- [x] 3.5 `./build.ps1 verify-platform-artifacts` with the outputs of 3.2 and 3.3 in `.output/`
      passes; deleting the `.tgz.sha256` makes it fail
- [x] 3.6 Check `publish.yml` upload globs still cover `.tgz` + `.sha256` (they already do; no edit
      expected)

## 4. Docs

- [x] 4.1 Add a `CHANGES.md` entry noting Linux/macOS release assets are now `.tgz`
- [x] 4.2 Update any README / doc install instructions that reference the Linux/macOS `.zip`
      asset names
