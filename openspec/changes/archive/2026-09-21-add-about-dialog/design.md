## Context

`lazynats` has no existing embedded-resource text, no existing "read version from the running
assembly" code, and no precedent for a fixed-width/scrollable-height dialog (every existing
dialog either fixes both width and height, like `MessageDetailDialog`'s per-section frames, or
grows height to fit content via `Dim.Auto`, also like `MessageDetailDialog` overall). This change
introduces all three as one small, low-risk feature: an About dialog reachable via `F10`.

Global shortcuts (`Alt+Q`, `Alt+1..5`, `Alt+0`, `Alt+P`, `?`) are wired in one place -
`MainWindow`'s `topLevelShortcuts` list - via `KeyDown` subscription on `MainWindow` itself, not
`Application.KeyBindings`/`BindKeyToApplication` (see the comment at `MainWindow.cs:161` on why:
app-level binding bypasses modal key-routing and can fire a shortcut out from under an open
dialog). Modal dialogs (`Alt+P`'s `PublishDialog`, the shortcut picker) are opened via
`App!.AddTimeout(TimeSpan.Zero, ...)` rather than `App!.Run(...)` directly inside the handler, to
avoid `Run`'s nested loop re-observing the still-unwinding keypress.

## Goals / Non-Goals

**Goals:**
- Prove the embedded-resource-text + `{token}` replacement + assembly-version-lookup mechanism
  end to end, in the simplest form that works (`string.Replace`, no template engine).
- Add a fixed-width, scrollable-height read-only text dialog, reusing the existing
  `TextView { ReadOnly = true }` + `ViewportSettingsFlags.HasVerticalScrollBar` scrolling pattern
  already used by `PayloadDetailSection`, rather than inventing a new scrolling mechanism.
- Wire `F10` as a global shortcut following the exact `MainWindow.topLevelShortcuts` /
  deferred-`AddTimeout` pattern already used for `Alt+P`.

**Non-Goals:**
- Polished About copy - the embedded text is placeholder ("generic product / version / author"
  per the request) and expected to be rewritten later without further code changes.
- A general-purpose templating engine - only literal `{token}` substitution via `string.Replace`.
- Making the dialog's width responsive to terminal size - it is deliberately fixed-width (per the
  request), unlike `MessageDetailDialog`'s `Dim.Func`-clamped width.
- Listing this shortcut in the status bar's dynamic tail beyond what `ShortcutHint` already gives
  it for free - no new `IShortcutSource` plumbing.

## Decisions

**Plain `Dialog`, not `Dialog<T>`.** Nothing outside the dialog needs a value back once it closes
- same reasoning `MessageDetailDialog`'s own header comment gives for the identical choice.
  Buttonless: `Esc` closes it via the inherited cancellation convention every other buttonless
  dialog here already relies on (`PatternDialog`, `ShortcutPickerDialog`, `MessageDetailDialog`).

**Spacing follows the existing bordered-dialog conventions exactly, per `CLAUDE.md` and
`dialog-spacing`.** Title is `" About "` (leading/trailing space, matching every other dialog
title). `Padding.Thickness` is set for horizontal breathing room the same way `PatternDialog`
does (`new Thickness(1, 0, 1, 0)`) rather than letting the `EditFrame` butt against the dialog
border. Being a single-content, button-less dialog that commits/dismisses via `Esc` (no button
row), it is exempt from `dialog-spacing`'s "one blank row above the first control" rule -
matching `PatternDialog`/`HeaderDialog` - so it stays as vertically compact as they are rather
than gaining an unmatched top blank row.

**Fixed width via a plain integer, height via `Dim.Auto` clamped to the screen, content scrolls
past that.** Width is a constant (e.g. `60`) rather than `MessageDetailDialog`'s
`Dim.Func(Math.Min(preferred, screen.Width - margin))` - the request calls for fixed width
specifically, and About text has no reason to want extra horizontal room the way JSON payloads
do. Height stays `Dim.Auto` (Terminal.Gui's own built-in screen-percentage clamp already prevents
a pathologically tall dialog from overflowing the terminal, same as `MessageDetailDialog`), and
the text area itself gets a fixed row count sized to a reasonable default (e.g. 10 rows) rather
than growing with content - so text longer than that scrolls instead of the dialog itself growing
without bound.

**Content sits in an `EditFrame`-wrapped read-only view, reusing `EditFrame.CreateReadOnly` and
`PayloadDetailSection`'s existing scroll mechanics - not a raw `TextView`.** Per the request, the
text area gets the same `EditFrame` treatment every other block of text in this codebase gets
(`MessageDetailDialog`'s Subject/Headers, `PayloadDetailSection`'s payload view), for visual
consistency, even though About text is never edited. `EditFrame.CreateReadOnly(text, y, height,
out Label view)` already produces exactly this: a non-focusable frame around a multi-line,
non-word-wrapped `Label` on `Theme.EditableBackground`. Scrolling reuses
`PayloadDetailSection`'s pattern verbatim rather than introducing a `TextView`: when the text's
line count exceeds the visible rows, `view.SetContentHeight(lineCount)` and `view.ViewportSettings
|= ViewportSettingsFlags.HasVerticalScrollBar` turn on Terminal.Gui's generic per-`View` viewport
scrolling (not `TextView`-specific), and the dialog binds `Command.ScrollUp/ScrollDown/PageUp/
PageDown` to `Key.CursorUp/CursorDown/PageUp/PageDown`, each calling `view.ScrollVertical(...)` -
the same four-command `BindScrollKeys` shape `PayloadDetailSection` already has. This keeps About
consistent with every other read-only text block in the app instead of introducing a second,
`TextView`-based scrolling mechanism alongside the existing `Label`-viewport one.

**Embedded resource text file, loaded via `Assembly.GetManifestResourceStream` +
`GetExecutingAssembly`.** Marked `<EmbeddedResource>` in `lazynats.csproj` under a new
`src/lazynats/About/About.txt` (or similar) rather than a `.resx` - a `.resx` gives no benefit
here (no localization, no non-string resources) and adds indirection for no reason;
`.csproj`-marked embedded text files are already how `.NET` conventionally ships small readonly
text assets, and AOT-publish it cleanly since embedded resources are baked into the binary itself
(no reflection-based resource-manager lookup needed - a plain `GetManifestResourceStream` by
fully-qualified name).

**Token replacement is exactly `string.Replace`, called once per known token, not a loop over an
arbitrary dictionary.** Per the request ("just use `string.Replace`"): a small private helper
does `text.Replace("{version}", version)` (and similarly for any other token the placeholder text
uses), not a generalized template engine. This keeps the mechanism AOT-trivial and matches
`CLAUDE.md`'s domain-named-wrapper convention (e.g. `RenderAboutText(rawText)` as the one place
this happens) without over-generalizing into a reusable templating abstraction nothing else needs
yet.

**Version comes from `AssemblyInformationalVersionAttribute` (falling back to
`Assembly.GetName().Version`), read off `typeof(MainWindow).Assembly` (or an equivalent
already-loaded type in the entry assembly), not a hand-maintained constant.** This is the
existing `dotnet`-idiomatic way to surface a build's version at runtime, and matches
`platform-release-builds`' existing GitVersion-driven versioning without needing to plumb
GitVersion's output into the app itself - a local `dotnet run`/`dotnet build` with no version
set will simply show `.NET`'s own default (e.g. `1.0.0.0`), which is acceptable for this
placeholder pass.

**`F10` is bound exactly like `Alt+P`: appended to `MainWindow.topLevelShortcuts`, opened via the
same deferred `App!.AddTimeout(TimeSpan.Zero, () => App!.Run(new AboutDialog()))`.** No new
binding mechanism - this is the same re-entrancy hazard Publish already solved (see
`MainWindow.cs:123-135`'s comment), and reusing it keeps every global, dialog-opening shortcut in
one place and one style. It also gets the status bar for free: `topLevelWidgets` maps every
entry in `topLevelShortcuts` (`ShortcutHint.Text`/`.Key`) straight into a `Shortcut` handed to
`StatusBar`, so adding `F10`/"About" to that one list is both the key binding and the status-bar
label - there is no second place to register it, and no separate step could omit it by accident.

## Risks / Trade-offs

- **Fixed dialog width could clip on a very narrow terminal.** → Accepted: About text is short
  and this is explicitly a fixed-width design per the request; other dialogs already tolerate
  narrow terminals inconsistently (e.g. `PatternDialog`'s fixed `Width = 43`), so this isn't a new
  class of problem.
- **Version reads as `1.0.0.0`/unset outside of a GitVersion-stamped release build.** → Accepted
  for this pass - the request explicitly says generic placeholder text is fine and polish comes
  later; a real release build already gets a real version via the existing Nuke/GitVersion
  pipeline (`platform-release-builds`), so this only affects local `dotnet run` during
  development.
- **A missing/renamed embedded resource name throws at startup-of-dialog rather than at compile
  time.** → Mitigation: the resource name is a single `const string` right next to the
  `GetManifestResourceStream` call, and the dialog is manually exercised (via `tmux`, per
  `CLAUDE.md`) before considering the task done, which would surface a typo immediately.

## Open Questions

- Exact wording of the placeholder About text is left to implementation (product name, a
  generic "Author: TBD" line, `{version}` token) - the request says this will be polished later,
  so any reasonable generic text satisfies this change.
