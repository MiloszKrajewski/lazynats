# color-theme Specification

## Purpose

The app applies a single, centrally-declared black/dark-gray color theme across the whole UI —
including Terminal.Gui's built-in dialog scheme — so the background, editor-control background,
and dialog rendering are explicit and consistent rather than dependent on the terminal emulator's
own default rendering or Terminal.Gui's stock blue-toned dialog colors.

## Requirements

### Requirement: App-wide base color theme
The system SHALL apply a single, app-wide base color theme at startup, so the app's background
and editor-control background are explicit and consistent rather than dependent on the terminal
emulator's own default rendering. The app background SHALL use `ColorName16.Black` as a neutral
for static controls; the editor-control background SHALL use a single, centrally-declared
truecolor constant chosen darker than `ColorName16.DarkGray`, so it reads as distinct from the
background without hurting text legibility, and SHALL resolve to `ColorName16.Black` on a
terminal without truecolor support.

#### Scenario: App background is explicit black
- **WHEN** the application starts and any view resolves its `VisualRole.Normal` color (directly,
  or by inheriting the shared base scheme without setting its own)
- **THEN** the resolved background is `ColorName16.Black`, regardless of the host terminal's own
  default background setting

#### Scenario: Editor controls use a distinct background
- **WHEN** any editable control (e.g. a `TextField`, `TextView`, or `EditFrame`-wrapped input)
  resolves its `VisualRole.Editable` color, whether the control currently has focus or not
- **THEN** the resolved background is the app's single editor-background constant (a truecolor
  value darker than `ColorName16.DarkGray`), read from its one declared location

#### Scenario: Editor background does not change with focus
- **WHEN** an editable control transitions between focused and unfocused
- **THEN** its `VisualRole.Editable` background remains the same editor-background constant in
  both states — no separate focused-state background is applied for this role

#### Scenario: Editor background falls back to black without truecolor support
- **WHEN** the editor-background constant is resolved on a terminal/driver that does not support
  truecolor rendering
- **THEN** it degrades to `ColorName16.Black` (confirmed via
  `Color.GetClosestNamedColor16()` for the chosen value), not `ColorName16.DarkGray` or any other
  palette entry

#### Scenario: Existing per-view color overrides still take precedence, using the same background
- **WHEN** a view has its own explicit `Scheme`/`Attribute` override (e.g. the invalid-input
  highlighting in `PatternDialog`, `HeaderDialog`, or `PublishView`)
- **THEN** that view's own override is still used instead of the base theme's colors, and its
  background component is the same single editor-background constant (not an independently
  hardcoded value that could drift out of sync with it)

#### Scenario: An invalid, focused field's background matches its valid-state background
- **WHEN** an editable control (e.g. `PublishView`'s Subject field) is invalid (its `Trim()`-empty
  default state) and has focus, so its cursor cell is the only thing rendered
- **THEN** that cell's background is the same editor-background color as the valid state — an
  invalid-state override changes only the foreground (e.g. to red), never the background

#### Scenario: A wrapped edit control's full content area shows the editor background
- **WHEN** an `EditFrame`-wrapped control (e.g. `PublishView`'s Subject/Headers/Payload fields)
  does not paint its entire bounds itself (e.g. an empty `TextField`, which only paints under its
  actual text)
- **THEN** the cells the control leaves unpainted still show the editor-background color, not the
  app's background color

#### Scenario: A list editor's empty-state hint uses the editor background
- **WHEN** a `ListEditorView<T>` with its `Background` set (e.g. `HeaderEditorView`) has no items,
  so its empty-state hint label fully covers the list
- **THEN** the hint label's background is the same editor-background color as the list itself,
  not an unrelated (e.g. `Disabled`-role) background

### Requirement: Dialogs match the app's base color theme
The system SHALL apply the same black/gray palette to Terminal.Gui's built-in `"Dialog"` scheme
(used by `Dialog`-derived views and `MessageBox`), so dialogs render visually consistent with the
rest of the app instead of using Terminal.Gui's stock blue-toned dialog colors.

#### Scenario: Dialog background is black
- **WHEN** a `Dialog` (e.g. `PatternDialog`, `HeaderDialog`) or a `MessageBox` resolves its
  `VisualRole.Normal` color
- **THEN** the resolved background is `ColorName16.Black`, not the stock blue-toned background

#### Scenario: Editable fields inside a dialog use the app's editor background
- **WHEN** an editable control inside a `Dialog` (e.g. `PatternDialog`'s or `HeaderDialog`'s text
  field) resolves its `VisualRole.Editable` color
- **THEN** the resolved background is the same single editor-background constant used elsewhere
  in the app, and remains distinguishable from the dialog's own `Normal` background

#### Scenario: A dialog's field background is the same whether valid or invalid
- **WHEN** `PatternDialog`'s or `HeaderDialog`'s field is empty/invalid (its default state) versus
  when it holds valid text
- **THEN** the field's full width shows the same editor-background color in both states — only
  the foreground (e.g. red for invalid) differs, never the background falling back to the
  dialog's own `Normal` color in either state

#### Scenario: Dialog focus highlight is no longer blue-tinted
- **WHEN** a focusable element inside a `Dialog` resolves its `VisualRole.Focus` color
- **THEN** the resolved colors no longer include the stock blue tones (`LightSkyBlue`/
  `OuterSpace`)
