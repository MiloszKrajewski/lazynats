## MODIFIED Requirements

### Requirement: App-wide base color theme
The system SHALL apply a single, app-wide base color theme at startup, so the app's background
and editor-control background are explicit and consistent rather than dependent on the terminal
emulator's own default rendering. The app background SHALL use `ColorName16.Black` as a neutral
for static controls; the editor-control background SHALL use a single, centrally-declared
truecolor constant chosen darker than `ColorName16.DarkGray`, so it reads as distinct from the
background without hurting text legibility, and SHALL resolve to `ColorName16.Black` on a
terminal without truecolor support. This applies to every editable control, including a
`DropDownList<T>`, even though that control internally redirects `VisualRole.Editable` attribute
lookups to `VisualRole.Normal`/`VisualRole.Focus` in its default (read-only) mode rather than
resolving `Editable` directly.

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

#### Scenario: A closed, unfocused DropDownList uses the editor background
- **WHEN** a `DropDownList<T>` (e.g. Ack Policy / Deliver Policy in `CreateConsumerDialog`,
  Retention in `CreateStreamDialog`) is closed and does not currently have focus
- **THEN** its rendered background is the same single editor-background constant as an unfocused
  `TextField` — not the ambient `Normal` background, which is what the control's default redirect
  from `VisualRole.Editable` to `VisualRole.Normal` would otherwise produce

#### Scenario: A closed, focused DropDownList shows an inverted highlight bar
- **WHEN** a `DropDownList<T>` is closed and gains focus
- **THEN** its rendered colors invert (the editor-background constant becomes the foreground, its
  paired white foreground becomes the background) rather than staying identical to the unfocused
  state or picking up the dialog's stock `Focus` highlight — the same swapped-foreground/background
  bar a selected row in a `ListEditorView`/`DrillableListView`-based list gets, since a `TextField`
  gets its own focus affordance from its blinking cursor but a read-only `DropDownList` has none

#### Scenario: An EditFrame wrapping a DropDownList matches the control's unfocused background
- **WHEN** a `DropDownList<T>` is wrapped in an `EditFrame` the same way a `TextField` is (via
  `WrapField`, reading the wrapped control's `VisualRole.Editable` attribute at construction time,
  before the control can have focus)
- **THEN** the frame's inner rule color matches the `DropDownList<T>`'s closed, unfocused
  background, and stays that color even while the control is focused — the frame does not track
  the focused-state highlight bar, the same way an `EditFrame`-wrapped list's border doesn't track
  which row inside it is currently selected/highlighted

#### Scenario: An expanded DropDownList popup uses the editor background
- **WHEN** a `DropDownList<T>`'s popup list is expanded
- **THEN** the popup's background is the same single editor-background constant as the control's
  own closed-state background, not the ambient background of whatever view last held focus
