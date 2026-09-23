## MODIFIED Requirements

### Requirement: Each platform job publishes its zip as a build artifact
Each of the four platform jobs in `publish.yml` SHALL upload the release archive produced by its
Nuke target (`.zip` or `.tgz`, whichever the target emits) together with its `.sha256` sidecar as a
GitHub Actions build artifact, for the `publish` job to consume.

#### Scenario: A platform job completes successfully
- **WHEN** a platform job's Nuke target finishes producing its archive
- **THEN** the job uploads that archive file and its `.sha256` sidecar as a build artifact before
  completing

### Requirement: PublishToGitHub uploads platform release zips
The `PublishToGitHub` Nuke target SHALL select the platform release archives
(`lazynats-<version>-<system>-<arch>.zip` and `lazynats-<version>-<system>-<arch>.tgz` in the
collected output directory, matching the version resolved from `CHANGES.md`), plus each archive's
`.sha256` sidecar, as the assets to attach to the GitHub Release. `VerifyPlatformArtifacts` SHALL
apply the same selection when checking that archives and their checksums are present.

#### Scenario: PublishToGitHub runs with platform archives present
- **WHEN** `PublishToGitHub` runs with the Windows `.zip` and the Linux/macOS `.tgz` archives
  present in its collected output directory
- **THEN** all of them, and their `.sha256` sidecars, are attached as assets to the created GitHub
  Release

#### Scenario: A tarball is missing its checksum
- **WHEN** `VerifyPlatformArtifacts` finds a `.tgz` release archive with no matching `.sha256`
- **THEN** it fails, the same as it does for a `.zip` without one

#### Scenario: Re-running for a version that already has a release
- **WHEN** `PublishToGitHub` runs for a version whose GitHub Release already exists
- **THEN** it skips creating a new release and does not fail the workflow
