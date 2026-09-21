## 1. Embedded About text

- [x] 1.1 Create `src/lazynats/About/About.txt` with placeholder product/version/author text,
      including a literal `{version}` token (e.g. `lazynats\n\nVersion: {version}\nAuthor: TBD\n\nA terminal UI for NATS.`)
- [x] 1.2 Mark `src/lazynats/About/About.txt` as `<EmbeddedResource>` in `lazynats.csproj`

## 1a. Public assembly metadata (follow-up)

- [x] 1a.1 Fill in `PublicAssembly.props`' placeholder `Description`/`PackageTags`, and add
      `<Company>$(Authors)</Company>` (mirroring the `K4os.NatsTransit` repo's `PublicAssembly.props`
      pattern) so the About dialog has a build-time-generated `AssemblyCompanyAttribute` to read
- [x] 1a.2 `<Import Project="$(PublicAssemblyProps)" />` in `lazynats.csproj` (the entry assembly
      `AboutText.Load` reflects over), following the same per-csproj import `Directory.Build.props`
      already wires up via `$(PublicAssemblyProps)`
- [x] 1a.3 Replace `About.txt`'s hardcoded `lazynats`/`TBD`/description line with `{product}`/
      `{author}`/`{description}` tokens, and add matching resolution in `AboutText.cs` off
      `AssemblyProductAttribute`/`AssemblyCompanyAttribute`/`AssemblyDescriptionAttribute`

## 2. Version + token replacement helper

- [x] 2.1 Add a small private/internal helper (e.g. `AboutText.Load()`) in `src/lazynats/About/`
      that reads the embedded resource via `Assembly.GetManifestResourceStream` on the executing
      assembly, using the resource's fully-qualified name as a single `const string`
- [x] 2.2 Resolve the app version from `AssemblyInformationalVersionAttribute` on the entry/executing
      assembly, falling back to `Assembly.GetName().Version` if the attribute is absent
- [x] 2.3 Replace the literal `{version}` token via `string.Replace`, leaving any other
      `{...}`-shaped text in the resource untouched

## 3. AboutDialog view

- [x] 3.1 Create `src/lazynats/About/AboutDialog.cs`: a plain `Dialog` (not `Dialog<T>`), titled
      `" About "` (leading/trailing space, per `dialog-spacing`), fixed `Width` (constant, not
      `Dim.Func`/percentage), `Height` left as `Dim.Auto`
- [x] 3.2 Set `Padding.Thickness = new Thickness(1, 0, 1, 0)`, matching `PatternDialog` - a
      button-less, single-content dialog is exempt from `dialog-spacing`'s extra top blank row,
      so no additional top padding is added
- [x] 3.3 Use `EditFrame.CreateReadOnly(text, y, height, out Label view)` for the text area (not
      a raw `TextView`), populated with the token-replaced About text from step 2.3, so it gets
      the same framed look as `MessageDetailDialog`'s Subject/Headers and
      `PayloadDetailSection`'s payload view even though it's never edited
- [x] 3.4 Reuse `PayloadDetailSection`'s scroll mechanics on that `Label`: when its line count
      exceeds the visible rows, call `SetContentHeight(lineCount)` and OR in
      `ViewportSettingsFlags.HasVerticalScrollBar`; bind `Command.ScrollUp/ScrollDown/PageUp/
      PageDown` to `Key.CursorUp/CursorDown/PageUp/PageDown`, each calling
      `view.ScrollVertical(...)`, mirroring `PayloadDetailSection.BindScrollKeys`
- [x] 3.5 Confirm Esc closes the dialog via the inherited buttonless-dialog cancellation
      convention (no extra key binding needed, matching `PatternDialog`/`MessageDetailDialog`)

## 4. Global F10 shortcut

- [x] 4.1 In `MainWindow.cs`, append an `F10`/"About" entry to `topLevelShortcuts` that opens
      `AboutDialog` via the same deferred `App!.AddTimeout(TimeSpan.Zero, () => { App!.Run(new
      AboutDialog()); return false; })` pattern used for `Alt+P`/`PublishDialog`
- [x] 4.2 Confirm the status bar shows the `F10`/"About" hint automatically, since
      `topLevelWidgets` already projects every `topLevelShortcuts` entry into a `Shortcut` handed
      to `StatusBar` - no separate registration step

## 5. Docs

- [x] 5.1 Add an "About dialog" mention to `doc/UI.md` (shortcut: `F10`, fixed-width/scrollable
      modal, read-only)

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` compiles cleanly
- [x] 6.2 Drive the app via `tmux` (per `CLAUDE.md`): confirm the status bar shows the `F10`
      "About" hint, press `F10` from a management tab, confirm the dialog opens framed in an
      `EditFrame`, its width stays fixed, `{version}` is substituted, and `Esc` closes it
- [x] 6.3 Verify `F10` does not fire while another modal dialog (e.g. Publish via `Alt+P`) is
      already open
