## Why

`lazynats` has no way to check which build is running or see basic project attribution from
inside the app. A simple, keyboard-triggered About dialog gives users that at a glance, and gives
us a low-risk first embedded-resource + assembly-metadata pattern to build on.

## What Changes

- Add a global `F10` shortcut (same wiring pattern as `Alt+P`/`Alt+?` in `MainWindow`) that opens
  a modal About dialog.
- The dialog is fixed-width, auto/scrollable-height: content taller than the available height
  scrolls; width does not reflow with the terminal.
- Dialog text is loaded from an embedded resource text file (not hardcoded C# strings), so the
  wording can be edited without a code change.
- The embedded text supports trivial `{token}` replacement (e.g. `{version}`) via
  `string.Replace` - no templating engine.
- `{version}` resolves from assembly/product info (`AssemblyInformationalVersionAttribute` /
  `AssemblyName.Version`), not a hand-maintained constant.
- Initial embedded text is placeholder/generic (product name, version, author line) - wording is
  expected to be polished later; this change only needs to prove the mechanism.

## Capabilities

### New Capabilities
- `about-dialog`: a modal, read-only About dialog showing embedded, token-substituted text about
  the app (product/version/author), opened globally via F10.

### Modified Capabilities
(none - `about-dialog` is additive; no existing capability's requirements change)

## Impact

- New files under a new `src/lazynats/About/` folder: the dialog view, the embedded resource
  text file, and a small token-replacement/version-lookup helper.
- `lazynats.csproj`: mark the new text file as an `EmbeddedResource`.
- `MainWindow.cs`: one more entry in `topLevelShortcuts` (F10) following the existing
  deferred-`AddTimeout` pattern used for Publish/Shortcuts, plus a matching `doc/UI.md` mention.
