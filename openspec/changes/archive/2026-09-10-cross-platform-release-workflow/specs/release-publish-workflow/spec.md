## ADDED Requirements

### Requirement: Manually-triggered release workflow
`.github/workflows/release.yml` SHALL be triggerable only via `workflow_dispatch`, and SHALL NOT
run on push, pull request, or any other automatic trigger.

#### Scenario: Pushing to the repository
- **WHEN** commits are pushed to any branch
- **THEN** `release.yml` does not run

#### Scenario: Manually dispatching the workflow
- **WHEN** a repository maintainer triggers `release.yml` via `workflow_dispatch`
- **THEN** the workflow runs its platform build jobs and publish job

### Requirement: Per-platform jobs run on natively-matching runners
`release.yml` SHALL define one build job per supported platform, each running on a GitHub-hosted
runner whose OS and CPU architecture natively match that platform's target RID: `windows-x64` on
`windows-latest`, `linux-x64` on `ubuntu-latest`, `linux-arm64` on `ubuntu-24.04-arm`, and
`macos-arm64` on `macos-latest`. Each job SHALL invoke only its own matching Nuke release target.

#### Scenario: Each build job invokes its matching target on a matching runner
- **WHEN** `release.yml` runs its build jobs
- **THEN** the `windows-x64` job runs `release-windows-x64` on `windows-latest`
- **AND** the `linux-x64` job runs `release-linux-x64` on `ubuntu-latest`
- **AND** the `linux-arm64` job runs `release-linux-arm64` on `ubuntu-24.04-arm`
- **AND** the `macos-arm64` job runs `release-macos-arm64` on `macos-latest`

#### Scenario: No build job exercises the Docker fallback
- **WHEN** any of the four build jobs runs its Nuke target
- **THEN** the target takes its native-publish path (its runner's OS+arch matches the target),
  and no Docker container is built or run as part of that job

### Requirement: Linux jobs provision Native AOT build prerequisites via a workflow step
The `linux-x64` and `linux-arm64` jobs in `release.yml` SHALL install `clang` and `zlib1g-dev` via
a workflow step (e.g. `apt-get install`) before invoking their Nuke target, rather than the Nuke
target or build script installing system packages itself.

#### Scenario: Linux job prepares its runner before building
- **WHEN** the `linux-x64` or `linux-arm64` job runs
- **THEN** `clang` and `zlib1g-dev` are installed via an explicit workflow step
- **AND** that installation happens before the Nuke release target is invoked

### Requirement: Each platform job publishes its zip as a build artifact
Each of the four platform jobs in `release.yml` SHALL upload the zip produced by its Nuke target
as a GitHub Actions build artifact, for the `publish` job to consume.

#### Scenario: A platform job completes successfully
- **WHEN** a platform job's Nuke target finishes producing its zip
- **THEN** the job uploads that zip file as a build artifact before completing

### Requirement: A single publish job creates one GitHub Release with all platform zips
`release.yml` SHALL define a `publish` job that depends on all four platform jobs, downloads each
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

### Requirement: PublishToGitHub uploads platform release zips
The `PublishToGitHub` Nuke target SHALL select the platform release zips
(`lazynats-<version>-<system>-<arch>.zip` in the collected output directory, matching the version
resolved from `CHANGES.md`) as the assets to attach to the GitHub Release, rather than the
`*.nupkg` glob it previously used.

#### Scenario: PublishToGitHub runs with platform zips present
- **WHEN** `PublishToGitHub` runs with the four platform zips present in its collected output
  directory
- **THEN** all four zips are attached as assets to the created GitHub Release

#### Scenario: Re-running for a version that already has a release
- **WHEN** `PublishToGitHub` runs for a version whose GitHub Release already exists
- **THEN** it skips creating a new release and does not fail the workflow
