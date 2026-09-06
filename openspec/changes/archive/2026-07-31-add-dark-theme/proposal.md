## Why

`lazynats` currently has no shared color theme: individual views (`PatternDialog`,
`HeaderDialog`, `PublishView`, `ListEditorView`) each hand-roll their own `Attribute`/`Scheme`
for one-off cases (mostly invalid-input highlighting), and everything else falls back to
Terminal.Gui's stock defaults. As the UI grows into the tabbed layout described in
`doc/ui-design.md`, there's no consistent, reusable definition of the app's base look (window
background vs. editor/input background). Establishing one small, centrally-defined dark theme
now — built only from Terminal.Gui's built-in 16-color palette for broad terminal compatibility —
gives every future view a shared foundation instead of repeating ad hoc color code.

## What Changes

- Introduce a small, central color theme definition with two roles:
  - **App background** — `ColorName16.Black`, a neutral for static/non-editable controls.
  - **Editor background** — `Theme.EditableBackground`, a single named `Color(32, 32, 32)`
    truecolor constant (declared once in `src/lazynats/Theme.cs`, deliberately darker than
    `ColorName16.DarkGray` for legibility), used for editable/input controls (text fields, list
    editors, edit frames), applied the same whether the control is focused, unfocused, or invalid
    (only the foreground changes for invalid state — never the background).
- Apply the theme as the default `Scheme` at the app root, by overriding Terminal.Gui's built-in
  `"Base"` scheme once in `Program.cs` (`Window` defaults `SchemeName = "Base"`, so every view
  inherits it — `MainWindow.cs` itself needs no changes).
- Also override the built-in `"Dialog"` scheme (used by `PatternDialog`, `HeaderDialog`, and any
  `MessageBox`), which today uses a blue-toned palette (`OuterSpace`/`LightSkyBlue`/`#054D7A`), so
  dialogs render with the same black/gray palette as the rest of the app instead of standing out
  in blue.
- Existing ad hoc per-view overrides (invalid-input highlighting in `PatternDialog`,
  `HeaderDialog`, `PublishView`) stay in place and now reference `Theme.EditableBackground` too,
  so they can't drift out of sync with the rest of the theme.
- Wrap `PatternDialog`'s and `HeaderDialog`'s single text field in `EditFrame` (mirroring
  `PublishView`'s fields), and extend `EditFrame`'s own fill to cover its full content area — a
  bare/partially-painted `TextField` otherwise only colors the cells under its actual characters,
  leaving the rest showing through to whatever's behind it.

## Capabilities

### New Capabilities
- `color-theme`: central definition and app-wide application of the base color theme (app
  background and editor background roles).

### Modified Capabilities
_None — this is additive; no existing capability's requirements change._

## Impact

- `src/lazynats/Theme.cs` (new) — the single declared `Theme.EditableBackground` constant.
- `src/lazynats/Program.cs` — applies the `"Base"`/`"Dialog"` scheme overrides at startup; no
  changes needed in `MainWindow.cs` since it inherits `"Base"` by default.
- `src/lazynats/Components/EditFrame.cs` — fills its full content area, not just the border
  columns, so a control that doesn't paint its whole bounds still reads as editable.
- `src/lazynats/Components/ListEditorView.cs` — the empty-state hint overlay now follows the same
  `Background` property as the list itself.
- `src/lazynats/PublishView.cs`, `HeaderDialog.cs`, `Subscriptions/PatternDialog.cs` — invalid-input
  `Attribute`s reference `Theme.EditableBackground`; `HeaderDialog`/`PatternDialog`'s fields are now
  wrapped in `EditFrame`; all three explicitly re-set the `Editable` role on their invalid-state
  `Scheme` (a `new Scheme(Attribute)` derivation quirk otherwise silently drops the background).
- No new dependencies; uses existing `Terminal.Gui.Drawing.Scheme`/`Attribute`/`Color`/
  `ColorName16` APIs.
