# release-publish-workflow Specification

## Purpose
Defines the GitHub Actions release workflow (`.github/workflows/publish.yml`) that builds
`lazynats`'s platform release archives on natively-matching runners and publishes them together
as a single GitHub Release, plus the `PublishToGitHub` Nuke target it drives for that final
publish step.

## Requirements

### Requirement: Manually-triggered release workflow
`.github/workflows/publish.yml` SHALL be triggerable only via `workflow_dispatch`, and SHALL NOT
run on push, pull request, or any other automatic trigger.

#### Scenario: Pushing to the repository
- **WHEN** commits are pushed to any branch
- **THEN** `publish.yml` does not run

#### Scenario: Manually dispatching the workflow
- **WHEN** a repository maintainer triggers `publish.yml` via `workflow_dispatch`
- **THEN** the workflow runs its platform build jobs and publish job

### Requirement: Per-platform jobs run on natively-matching runners
`publish.yml` SHALL define one build job per supported platform, each running on a GitHub-hosted
runner whose OS and CPU architecture natively match that platform's target RID: `windows-x64` on
`windows-latest`, `linux-x64` on `ubuntu-latest`, `linux-arm64` on `ubuntu-24.04-arm`, and
`macos-arm64` on `macos-latest`. Each job SHALL invoke only its own matching Nuke release target.

#### Scenario: Each build job invokes its matching target on a matching runner
- **WHEN** `publish.yml` runs its build jobs
- **THEN** the `windows-x64` job runs `release-windows-x64` on `windows-latest`
- **AND** the `linux-x64` job runs `release-linux-x64` on `ubuntu-latest`
- **AND** the `linux-arm64` job runs `release-linux-arm64` on `ubuntu-24.04-arm`
- **AND** the `macos-arm64` job runs `release-macos-arm64` on `macos-latest`

#### Scenario: No build job exercises the Docker fallback
- **WHEN** any of the four build jobs runs its Nuke target
- **THEN** the target takes its native-publish path (its runner's OS+arch matches the target),
  and no Docker container is built or run as part of that job

### Requirement: Linux jobs provision Native AOT build prerequisites via a workflow step
The `linux-x64` and `linux-arm64` jobs in `publish.yml` SHALL install `clang` and `zlib1g-dev` via
a workflow step (e.g. `apt-get install`) before invoking their Nuke target, rather than the Nuke
target or build script installing system packages itself.

#### Scenario: Linux job prepares its runner before building
- **WHEN** the `linux-x64` or `linux-arm64` job runs
- **THEN** `clang` and `zlib1g-dev` are installed via an explicit workflow step
- **AND** that installation happens before the Nuke release target is invoked

### Requirement: Each platform job publishes its archive as a build artifact
Each of the four platform jobs in `publish.yml` SHALL upload the release archive produced by its
Nuke target (`.zip` or `.tgz`, whichever the target emits) together with its `.sha256` sidecar as a
GitHub Actions build artifact, for the `publish` job to consume.

#### Scenario: A platform job completes successfully
- **WHEN** a platform job's Nuke target finishes producing its archive
- **THEN** the job uploads that archive file and its `.sha256` sidecar as a build artifact before
  completing

### Requirement: A single publish job creates one GitHub Release with all platform zips
`publish.yml` SHALL define a `publish` job that depends on all four platform jobs, downloads each
of their uploaded zips, and runs the `PublishToGitHub` Nuke target to create a single GitHub
Release containing all four zips as release assets. If any platform job fails, the `publish` job
SHALL NOT run, and no GitHub Release SHALL be created for that workflow run.

#### Scenario: All four platform jobs succeed
- **WHEN** the `windows-x64`, `linux-x64`, `linux-arm64`, and `macos-arm64` jobs all complete
  successfully
- **THEN** the `publish` job runs, downloads all four zips, and creates one GitHub Release with
  all four zips attached as assets

#### Scenario: A platform job fails
- **WHEN** any of the four platform jobs fails
- **THEN** the `publish` job does not run
- **AND** no GitHub Release is created for that workflow run

### Requirement: PublishToGitHub uploads platform release archives
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
