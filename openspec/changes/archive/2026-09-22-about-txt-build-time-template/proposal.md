## Why

`AboutText.cs` resolves `{product}`/`{version}`/`{author}`/`{description}` at runtime via
`Assembly`-attribute reflection every time the About dialog opens, even though every one of those
values is already fixed by the time the assembly is built (from `PublicAssembly.props` and the
release version). The reflection step buys nothing - it's just a slower, harder-to-follow way to
read values that were already baked into the assembly at build time. Resolving the placeholders
once, at build time, into the actual embedded resource removes an entire class of runtime lookup
code for no behavior change in the shipped app.

## What Changes

- Split the current `About.txt` into two files: `About.template.txt` (checked in, holds the
  `{product}`/`{version}`/`{author}`/`{description}` placeholders - the file a contributor edits)
  and `About.txt` (checked in, the rendered result - the file actually embedded as a resource and
  shown in the dialog).
- Add a Nuke target that renders `About.template.txt` into `About.txt` by substituting the same
  four tokens from `PublicAssembly.props`' MSBuild properties and the release version, and wire it
  to run as part of the existing `Build` target so `About.txt` is refreshed on every Nuke build
  (including `Release`).
- **BREAKING** (internal only): `AboutText.cs` drops all token-substitution logic
  (`RenderAboutText`/`ResolveProduct`/`ResolveVersion`/`ResolveAuthor`/`ResolveDescription`) and
  the `Assembly`-attribute reflection it relied on; `Load()` becomes a straight read of the
  embedded resource's text, unmodified.
- A plain `dotnet build`/`dotnet run` (outside Nuke) keeps using whatever `About.txt` is currently
  committed - it is not regenerated - so it always shows the values from the last Nuke build,
  never raw `{token}` placeholders.

## Capabilities

### Modified Capabilities
- `about-dialog`: the About text's placeholders are resolved once, at build time, by a Nuke
  target rendering `About.template.txt` into the committed `About.txt`, instead of via runtime
  reflection over assembly attributes each time the dialog opens. The dialog now displays the
  embedded resource verbatim, with no runtime substitution step and no "unrecognized token" case
  to speak of at runtime.

## Impact

- `src/lazynats/About/About.txt` (existing, becomes the build-generated/committed output)
- `src/lazynats/About/About.template.txt` (new, the edited source)
- `src/lazynats/About/AboutText.cs` (simplified: drop reflection-based substitution)
- `.nuke/build/Program.cs` (new target rendering the template, wired into `Build`)
