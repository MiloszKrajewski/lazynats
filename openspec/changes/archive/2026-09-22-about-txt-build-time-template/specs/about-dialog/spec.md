## REMOVED Requirements

### Requirement: About text supports literal token replacement
**Reason**: Token substitution now happens once, at build time - a Nuke target renders
`About.template.txt` into `About.txt` - instead of at runtime each time the dialog opens. The
embedded resource the dialog loads already has every token resolved, so there is no runtime
substitution step left to specify.
**Migration**: None for end users - the dialog's displayed text is unchanged. Contributors editing
the About text now edit `About.template.txt` (using the same `{product}`/`{version}`/`{author}`/
`{description}` tokens) instead of editing `About.txt` directly.

## ADDED Requirements

### Requirement: About text template is rendered at build time
`About.template.txt` (holding the literal `{product}`/`{version}`/`{author}`/`{description}`
placeholders) SHALL be rendered into `About.txt` - the file embedded as a resource and displayed
by the About dialog - by a Nuke build target, substituting each token with its build-time value
(from `PublicAssembly.props`' MSBuild properties, and the release version), rather than at runtime
when the dialog opens.

#### Scenario: Nuke Build target renders the template
- **WHEN** the Nuke `Build` target (or any target depending on it, e.g. `Release`) runs
- **THEN** `About.txt` is regenerated from `About.template.txt` with all four tokens substituted
  with their current build-time values

#### Scenario: Plain dotnet build does not re-render the template
- **WHEN** the application is built or run via a plain `dotnet build`/`dotnet run` outside Nuke
- **THEN** `About.txt` is not regenerated, and the About dialog displays whichever values were
  baked in by the most recently run Nuke build

### Requirement: About dialog displays the embedded resource verbatim
The About dialog SHALL display the embedded `About.txt` resource's text exactly as read, with no
runtime token substitution or other transformation applied.

#### Scenario: Dialog shows pre-rendered text unchanged
- **WHEN** the About dialog is opened
- **THEN** the displayed text is identical to the embedded resource's contents, with no further
  substitution performed at display time
