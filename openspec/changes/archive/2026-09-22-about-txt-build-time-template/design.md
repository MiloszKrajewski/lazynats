## Context

`About.txt` today is checked in with literal `{product}`/`{version}`/`{author}`/`{description}`
tokens, embedded as a resource, and rendered by `AboutText.cs` at dialog-open time via
`string.Replace` fed from `Assembly` attribute reflection (`AssemblyProductAttribute`,
`AssemblyInformationalVersionAttribute`, `AssemblyCompanyAttribute`,
`AssemblyDescriptionAttribute`). Those attributes are themselves generated at build time from
`PublicAssembly.props`' `Product`/`Description`/`Company` MSBuild properties plus whatever version
Nuke's `Build` target stamps via `.SetVersion(PackageVersion.ToString())`. So the values are fixed
long before the dialog ever opens - the runtime reflection step is pure overhead.

The `Build` target's `DotNetBuild` call already has every value the tokens need in scope
(`Solution` project properties, `PackageVersion`), which makes template rendering a natural fit
there rather than a new standalone tool.

## Goals / Non-Goals

**Goals:**
- Resolve `{product}`/`{version}`/`{author}`/`{description}` exactly once, at Nuke build time,
  writing the result into the same `src/lazynats/About/About.txt` path that's already embedded as
  a resource today - no change to the embedding or dialog-loading path.
- Remove `AboutText.cs`'s reflection-based substitution entirely; `Load()` becomes a plain resource
  read.
- Keep `About.template.txt` as the only file a contributor edits by hand.

**Non-Goals:**
- Making the substitution available under a plain `dotnet build`/`dotnet run` outside Nuke. Per
  the chosen design, local dev without Nuke keeps showing whichever values were baked in by the
  last Nuke run (see Risks below) - this mirrors how `AssemblyInformationalVersionAttribute`
  already only gets a real value from a Nuke build today (`ResolveVersion`'s existing fallback
  comment).
- A generic templating engine or multi-file template system - still plain `{token}` replacement,
  just relocated from runtime to build time.

## Decisions

- **New Nuke target `GenerateAbout`, made a dependency of `Build`.** Placing it as
  `.DependsOn(GenerateAbout)` on the existing `Build` target (rather than a separate manually-run
  target) means every path that already runs `Build` - plain `Build`, `Rebuild`, `Release`, and
  everything downstream of `Release` - regenerates `About.txt` for free, with no new step for
  contributors or CI to remember to invoke.
  - Alternative considered: hook it into `Restore` instead. Rejected - `Restore` conceptually
    fetches dependencies, not generated sources, and `Build` is where `PackageVersion` is already
    consumed (`.SetVersion(PackageVersion.ToString())`), so both values are already in scope in
    the same place.
- **Read `Product`/`Description`/`Company` (Authors) via `Project.GetProperty<string>` on the app
  project, and `{version}` from the existing `PackageVersion` NuGetVersion.** These are the exact
  same MSBuild-evaluated values `AssemblyProductAttribute`/etc. already surface today, just read
  one layer earlier (project properties instead of the compiled attributes), so the rendered text
  is identical to what the runtime reflection path used to produce.
- **`GenerateAbout` writes straight to the committed `src/lazynats/About/About.txt`, not to an
  `obj/`/intermediate path.** This was the explicit trade-off accepted over an MSBuild-target
  approach: `About.txt` stays the actual file `EmbeddedResource` points at and the one shown by a
  plain `dotnet build`, at the cost of it only being current as of the last Nuke run (see Risks).
- **`AboutText.cs` keeps its `Load()` method and embedded-resource path unchanged, but drops every
  `Resolve*`/`RenderAboutText` method.** No public surface changes - `AboutDialog.cs` still calls
  `AboutText.Load()` and gets back ready-to-display text.

## Risks / Trade-offs

- **Staleness between Nuke runs** → A contributor who edits `PublicAssembly.props` (e.g. bumps
  `Description`) and only runs `dotnet build`/`dotnet run` won't see the change reflected in the
  About dialog until a Nuke `Build` (or higher) runs and re-renders `About.txt`. Mitigated by this
  being no worse than today's existing version-stamping story (`ResolveVersion`'s local-build
  fallback), and by `About.txt` being a small, obviously-generated file that a diff makes visible
  in review.
- **Generated file is committed, so diffs show up on every version bump** → Every `Release` run
  changes `About.txt`'s `Version:` line, which will appear in the PR/commit that ran it. Accepted
  as consistent with `CHANGES.md`-driven versioning already producing visible diffs elsewhere.
- **Forgetting to run any Nuke target before a fresh clone's first `dotnet run`** → `About.txt` is
  still checked into git (not gitignored), so a fresh clone shows the values from whenever it was
  last regenerated, never raw `{token}` placeholders or a missing-resource error.

## Migration Plan

- Add `About.template.txt` (copy of current `About.txt`'s token layout).
- Add the `GenerateAbout` Nuke target and wire it into `Build`.
- Run `./build.ps1 Build` once locally to regenerate and commit the real `About.txt` from the
  template.
- Simplify `AboutText.cs`.
- No rollback complexity: reverting the commit restores the old runtime-substitution behavior
  wholesale.
