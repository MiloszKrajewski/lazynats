## 1. Template file

- [x] 1.1 Add `src/lazynats/About/About.template.txt` with the current `{product}`/`{version}`/
      `{author}`/`{description}` placeholder layout copied from today's `About.txt`.

## 2. Nuke build target

- [x] 2.1 Add a `GenerateAbout` target to `.nuke/build/Program.cs` that reads
      `Product`/`Description`/`Company` (Authors) from the app project's MSBuild properties and
      `PackageVersion` for `{version}`, substitutes all four tokens into
      `About.template.txt`'s contents, and writes the result to
      `src/lazynats/About/About.txt`.
- [x] 2.2 Wire `GenerateAbout` as a dependency of the existing `Build` target
      (`.DependsOn(GenerateAbout)`).
- [x] 2.3 Run `./build.ps1 Build` locally to regenerate `About.txt` from the template and confirm
      the rendered values match what `AboutText.cs` currently produces at runtime.

## 3. Simplify AboutText.cs

- [x] 3.1 Remove `RenderAboutText`, `ResolveProduct`, `ResolveVersion`, `ResolveAuthor`,
      `ResolveDescription`, and the `System.Reflection` usage they depend on from
      `src/lazynats/About/AboutText.cs`.
- [x] 3.2 `Load()` reads the embedded resource stream and returns its text unmodified (no
      `RenderAboutText` call).
- [x] 3.3 Update the file's header comment (currently describing the `string.Replace`-based
      per-token design) to reflect that substitution now happens at build time, not here.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` and `dotnet run --project src/lazynats`, then open the
      About dialog (`F10`) and confirm it shows the same product/version/author/description text
      as before this change.
- [x] 4.2 Confirm `git diff` on `About.txt` after step 2.3 shows only the expected rendered
      values (no stray whitespace/line-ending changes from the substitution).
- [x] 4.3 Update `openspec/specs/about-dialog/spec.md` per the delta in this change once
      implementation is verified (handled by `openspec archive`).
