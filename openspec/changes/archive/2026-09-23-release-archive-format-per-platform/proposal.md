## Why

Every release archive is a `.zip` today, including the Linux and macOS ones, where `.zip` drops the
Unix executable bit on the `lazynats` binary and isn't the format users of those platforms expect
(`tar xzf` rather than `unzip`). Nuke's `AbsolutePath.CompressTo` already writes `.tgz` when given a
`.tgz` file name (via SharpZipLib, which `Nuke.Common` already brings in), so producing tarballs is
a matter of choosing the format per platform, not adding a new dependency or writing our own
archiver.

## What Changes

- `CompressToFresh` takes the archive format as an explicit choice (`.zip` or `.tgz`) and derives
  the archive's extension from it, instead of every call site hard-coding `.zip`. It keeps
  delegating to Nuke's `CompressTo`, which dispatches on that extension.
- The noarch `Release` archive and `release-windows-x64` keep producing `.zip`.
- `release-linux-x64`, `release-linux-arm64` and `release-macos-arm64` produce `.tgz` instead of
  `.zip`, so the `lazynats` binary is executable after extraction (including when `release-linux-*`
  falls back to the Docker path from a Windows host).
- The `.sha256` sidecar is still written for every archive, whichever format.
- `PlatformArtifactsPattern` (used by `VerifyPlatformArtifacts` and `PublishToGitHub`) matches both
  `.zip` and `.tgz` release archives, so the Linux/macOS assets are still verified and uploaded.
- **BREAKING** (for downstream consumers only): Linux and macOS release asset names change from
  `lazynats-<version>-<system>-<arch>.zip` to `lazynats-<version>-<system>-<arch>.tgz`. Anything
  that downloads them by exact name (install scripts, docs) needs updating.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `platform-release-builds`: Linux/macOS targets emit `.tgz` (binary executable after extraction)
  instead of `.zip`; Windows and noarch stay `.zip`; the symbol-stripping and overwrite
  requirements apply to "the release archive" in either format; the noarch archive name is
  corrected to the `lazynats-<version>-noarch.zip` the build actually produces.
- `release-publish-workflow`: platform jobs upload, and `PublishToGitHub` attaches, release archives
  in either format (`.zip` or `.tgz`) rather than zips only.

## Impact

- `.nuke/build/Program.cs`: `CompressToFresh` signature, the four call sites,
  `PlatformArtifactsPattern`.
- No package changes: `Nuke.Utilities.IO.Compression` (and its SharpZipLib dependency) already
  arrive with `Nuke.Common`.
- `.github/workflows/publish.yml` already uploads `.output/*.tgz`; no workflow change expected
  beyond confirming that.
- GitHub Release asset names for Linux/macOS change extension.
