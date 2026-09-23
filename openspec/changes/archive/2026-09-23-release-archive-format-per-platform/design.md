## Context

`.nuke/build/Program.cs` has one archiving helper, `CompressToFresh(sourceDirectory, zipFile)`,
which deletes any previous archive, calls Nuke's `AbsolutePath.CompressTo` and writes a `.sha256`
sidecar. All four call sites (noarch `Release`, `PublishNative` for windows/linux/macos,
`PublishLinuxViaDocker`) pass a `.zip` name. `PlatformArtifactsPattern` (`*-{version}-*.zip`)
drives both `VerifyPlatformArtifacts` and `PublishToGitHub`.

`CompressTo` (`Nuke.Utilities.IO.Compression`, already a transitive dependency of `Nuke.Common`)
dispatches on the archive file's extension (checked by decompiling 10.1.0):
- `.zip` → `ZipTo`, built on BCL `System.IO.Compression.ZipArchive`;
- `.tar.gz` / `.tgz` → `TarGZipTo`, built on SharpZipLib `TarArchive` + `GZipOutputStream`.

Its tar path names entries relative to the source directory with `/` separators and no wrapping
directory (same flat layout as the zip), and SharpZipLib's `TarEntry.CreateEntryFromFile` gives
every regular file a fixed mode of `0700` (`rwx------`) regardless of host OS or the file's real
permissions. Both writers open the archive with `FileMode.CreateNew`, which is why
`CompressToFresh` deletes first.

`publish.yml` already globs `.output/*.tgz` in every upload step, so the workflow is ready for
tarballs; only the Nuke side needs to change.

## Goals / Non-Goals

**Goals:**
- `.zip` for noarch and Windows, `.tgz` for Linux and macOS, both via Nuke's `CompressTo`.
- The `lazynats` binary is executable after `tar xzf`, including for tarballs assembled on a
  Windows host (Docker fallback path).
- Verification/upload globs pick up both formats.

**Non-Goals:**
- Writing our own zip/tar code, or calling SharpZipLib / `System.Formats.Tar` directly.
- Controlling per-file Unix modes beyond what `CompressTo` produces (see Risks).
- Changing archive layout (still flat), naming stem, or the `.sha256` format.
- Code signing, notarization, or macOS-specific packaging (`.pkg`/`.dmg`).

## Decisions

### Format as an enum parameter, extension derived from it
`CompressToFresh(AbsolutePath sourceDirectory, AbsolutePath outputDirectory, string baseName,
ArchiveFormat format)` (or equivalent), with a small `enum ArchiveFormat { Zip, Tgz }` mapped to
`.zip` / `.tgz`. The helper builds the file name and hands it to `CompressTo`, which picks the
writer from that extension, so the format choice and the extension can't disagree.
- *Alternative:* keep passing a full file name and let the extension alone carry the format.
  Rejected: the platform-to-format decision then hides in string literals at four call sites
  instead of being an explicit, named argument.

### Platform picks the format at the call site
`PublishNative` gains an `ArchiveFormat` argument; `ReleaseWindowsX64` passes `Zip`, the Linux and
macOS targets pass `Tgz`. `PublishLinuxViaDocker` always uses `Tgz`. Noarch `Release` uses `Zip`.
If the "which format for this platform" decision ends up repeated, it goes behind one small named
helper (e.g. `ArchiveFormatFor(rid)`) per the domain-named-wrapper convention.
- *Alternative:* derive from the host OS. Rejected: the Docker fallback builds a Linux artifact on
  a Windows host, so host OS is the wrong signal; the target platform is.

### Reuse Nuke's `CompressTo` for both formats
It's already there, already used, and already covers `.tgz`; its fixed `0700` mode happens to be
host-independent, which is exactly what makes the Docker-from-Windows tarball executable.
- *Alternative:* BCL `System.Formats.Tar` with explicit per-entry modes (`0755` for the binary).
  Rejected: reimplements something the existing dependency already does, for a permissions
  refinement nobody has asked for yet.

### Artifact glob covers both extensions
`PlatformArtifactsPattern` becomes the union of `*-{version}-*.zip` and `*-{version}-*.tgz` globs
(e.g. a method returning both). `VerifyPlatformArtifacts` and `PublishToGitHub` consume the
combined list; the per-archive `.sha256` logic is unchanged.

## Risks / Trade-offs

- [Tarball files are `0700`] → Accepted; see "Known limitation: tarball file modes" below.
- [Downstream consumers download Linux/macOS assets by exact `.zip` name] → Called out as
  **BREAKING** in the proposal; add a `CHANGES.md` note for the release that ships it.
- [A future Nuke version changes `CompressTo`'s extension dispatch or tar modes] → Covered by the
  verification tasks (inspect with `tar -tzvf`); Nuke is pinned in `_build.csproj`.

## Known limitation: tarball file modes

**Status: accepted.**

Nuke's `.tgz` path (SharpZipLib `TarEntry.CreateEntryFromFile`) writes every regular file with a
fixed mode of `0700` (`rwx------`), independent of the build host and the file's real permissions.
Consequences:

- Extracting as yourself (e.g. into `~/.local/bin`) works: the binary is executable, no `chmod`.
- Extracting as root into a shared location (e.g. `sudo tar xzf ... -C /usr/local/bin`) leaves the
  binary root-only; other users need a `chmod 755` afterwards.
- Because the mode is host-independent, tarballs built via the Docker fallback on Windows behave
  the same as ones built natively on Linux/macOS.

If this ever needs fixing: write the `.tgz` with SharpZipLib's `TarArchive` directly (same
dependency, already present via `Nuke.Common`), mirroring Nuke's `CompressTar` (entry names via
`GetUnixRelativePathTo`) but setting the application binary's `TarHeader.Mode` to `0755` (or
`0644`/`0755` for everything). Scope is `CompressToFresh`'s `Tgz` branch only.

## Migration Plan

Ship with the next version bump; add a `CHANGES.md` line noting Linux/macOS assets are now
`.tgz`. Rollback is reverting the `Program.cs` change; the workflow already handles both
extensions.

## Open Questions

- None blocking.
