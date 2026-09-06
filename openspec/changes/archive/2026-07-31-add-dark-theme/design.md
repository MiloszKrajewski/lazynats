## Context

Terminal.Gui v2 already routes every view's colors through a small set of named `VisualRole`s
(`Normal`, `Focus`, `Editable`, `Disabled`, ...) resolved from a `Scheme` looked up by
`SchemeName` (verified against the installed 2.4.17 package via reflection). `Window` defaults
`SchemeName` to `"Base"`; plain `View`/`TextField` leave `SchemeName` unset and inherit whatever
scheme their nearest `SchemeName`-bearing ancestor resolves to — ultimately `"Base"` for
everything in this app, since `MainWindow` is a `Window` and nothing currently overrides
`SchemeName` anywhere else.

Two things follow from this:
- `Scheme.Normal` in the built-in `"Base"` scheme is `Attribute(None, None)` — i.e. **unset**, so
  the app's background today is whatever the terminal emulator's own default background happens
  to be, not something `lazynats` controls. This is the actual gap the proposal is closing.
- `Scheme.Editable` in `"Base"` is already `Attribute(White, Gray)`, and `PublishView`/`EditFrame`
  already read it dynamically via `GetAttributeForRole(VisualRole.Editable)` rather than
  hardcoding a color (see `PublishView.cs:41,62` and `EditFrame.cs:97`). `Scheme` has no
  separate focus-state slot for `Editable` — it's one `Attribute` regardless of focus, which is
  exactly the "same whether focused or unfocused" behavior asked for.

Terminal.Gui's built-in `"Dialog"`/`"Error"`/`"Menu"` schemes already mix in colors well beyond
the 16-value `ColorName16` set (e.g. `LightSkyBlue`, `OuterSpace`, `RaisinBlack` — an extended
W3C/X11-style named palette). This proposal deliberately does **not** follow that pattern: the
chosen roles use only `ColorName16` values, for maximum rendering consistency across terminal
emulators (including ones with limited/legacy color support).

`PatternDialog` and `HeaderDialog` are both `Dialog` subclasses, so they (and any `MessageBox`)
resolve their colors from the built-in `"Dialog"` named scheme, not `"Base"`. Reflecting on the
installed package shows `"Dialog"` today is: `Normal` fg=`LightSkyBlue` bg=`OuterSpace`, `Focus`
fg=`OuterSpace` bg=`LightSkyBlue`, `Editable` fg=`LightSkyBlue` bg=`#054D7A` — an all-blue palette
distinct from `"Base"`, which is what currently makes dialogs look visually different (blue) from
the rest of the app.

## Goals / Non-Goals

**Goals:**
- Make the app's base background and editor-control background explicit and consistent, instead
  of depending on terminal-default rendering.
- Do this with a single, central point of application rather than per-view color plumbing.
- Keep the app background on `ColorName16.Black` (a "nothing to see here" neutral for static
  controls); use a single, easily tunable truecolor value for the editor-control background so it
  reads as distinct from the background without looking "shiny" or hurting text legibility.
- Keep `Editable` a single color regardless of focus state (matches `Scheme`'s own shape — no
  new mechanism needed).
- Make dialogs (`PatternDialog`, `HeaderDialog`, `MessageBox`) visually consistent with the rest
  of the app — same background palette — instead of Terminal.Gui's stock blue-toned `"Dialog"`
  scheme.

**Non-Goals:**
- No full theme system (no user-configurable themes, no light theme, no per-view theme
  switching). Just a fixed, hardcoded dark theme with one tunable constant.
- No behavioral changes to the existing ad hoc validity-state overrides (`InvalidSubject`,
  `InvalidHeaderAttribute`, `InvalidPatternAttribute` in `PublishView`/`HeaderDialog`/
  `PatternDialog`) — they still layer their own `Scheme`/`Attribute` on top per-field exactly as
  before; only their hardcoded background literal was repointed at `Theme.EditableBackground`
  (Decision 6), so they stay in sync with the rest of the theme.

## Decisions

1. **Override the built-in `"Base"` named scheme via `SchemeManager.AddScheme("Base", ...)`,
   once, at startup — rather than setting `Scheme`/`SchemeName` on `MainWindow` or individual
   views.**
   Because `Window` defaults to `SchemeName = "Base"` and everything else inherits from it, this
   is the single point that reaches the whole app. Alternative considered: set
   `MainWindow.SetScheme(...)` directly — rejected because it would require duplicating the rest
   of `"Base"`'s roles (`Focus`, `Disabled`, etc.) by hand instead of deriving from the existing
   scheme, and wouldn't cover any future `Toplevel` that isn't a child of `MainWindow`.
   - `Normal` → `Attribute(ColorName16.Gray, ColorName16.Black)` (foreground stays a legible
     light gray; background becomes explicit `Black` instead of unset).
   - `Editable` → `Attribute(ColorName16.White, EditableBackground)` where `EditableBackground` is
     a `Color(32, 32, 32)` truecolor constant (see Decision 5) — foreground unchanged from today's
     `"Base"` default; background is a custom very-dark-gray, not a `ColorName16` name.
   - All other roles (`Focus`, `HotNormal`, `Disabled`, ...) are left as `"Base"`'s existing
     values by cloning the current scheme and only overwriting `Normal`/`Editable`, so unrelated
     behavior (e.g. focus-highlight banding) doesn't change.
2. **Apply the override in `Program.cs`, right after `Application.Create()` and before
   `Run<MainWindow>()`.** `Application.Create()` is what initializes Terminal.Gui's configuration
   system (which is what populates `SchemeManager`'s built-in schemes in the first place);
   registering the override any earlier risks it being reset/reloaded by that initialization.
   Alternative considered: do it inside `MainWindow`'s constructor — rejected because it's app
   startup configuration, not view construction, and `Program.cs` already owns other
   process-wide setup (`Application.MaximumIterationsPerSecond`).
3. **No new abstraction/class for "the theme."** This is two `Attribute` assignments on one
   scheme; a dedicated `Theme` type or config file would be premature for a fixed, hardcoded
   two-role palette. Revisit if/when a second theme (e.g. light mode) is ever needed.
4. **Override the built-in `"Dialog"` scheme's `Normal`/`Focus`/`Editable` roles to exactly mirror
   the overridden `"Base"` scheme's corresponding roles**, rather than inventing a separate
   dialog-specific palette:
   - `Normal` → `Attribute(ColorName16.Gray, ColorName16.Black)` (was
     `LightSkyBlue`/`OuterSpace`).
   - `Focus` → `Attribute(ColorName16.Black, ColorName16.White)` (was
     `OuterSpace`/`LightSkyBlue`) — unchanged in shape from `"Base"`'s own `Focus` (a reverse-video
     highlight), just no longer blue-tinted.
   - `Editable` → `Attribute(ColorName16.White, EditableBackground)` (was `LightSkyBlue`/`#054D7A`),
     matching the same editor-background role used everywhere else.
   Rationale: the user's own framing was "dialogs should look like a regular window" — reusing
   `"Base"`'s exact `Attribute` values (rather than picking new dark-blue→black,
   light-blue→gray equivalents role-by-role) is the most literal way to satisfy that, and keeps
   only two background tones (`Black`, `EditableBackground`, plus `White`/`Gray` foregrounds) in
   play across the whole app. Alternative considered: map each blue by "darkness" independently
   (both `OuterSpace` and `#054D7A` read as "dark blue" and could both become `Black`) — rejected
   because that would make `"Dialog"`'s `Editable` role indistinguishable from its `Normal` role,
   losing the ability to see which field is an input inside a dialog (`PatternDialog`/
   `HeaderDialog` both have text fields). Other `"Dialog"` roles (`HotNormal`, `HotFocus`,
   `Active`, ...) are left as-is.
5. **Declare the editor-background color as a single named constant (`Theme.EditableBackground`
   in a new `src/lazynats/Theme.cs`, a plain `Color(32, 32, 32)` truecolor value), used by
   `Program.cs`'s `"Base"`/`"Dialog"` overrides *and* by the invalid-input `Attribute`s in
   `PublishView`/`HeaderDialog`/`PatternDialog` (Decision 6), instead of `ColorName16.DarkGray`.**
   `DarkGray` (118,118,118) turned out too bright in practice — legible text against it was harder
   to read, and it read as "shiny" rather than a quiet backdrop for input. `(32, 32, 32)` is dark
   enough to stay quiet while still reading as distinct from the pure-black (`0,0,0`) app
   background. A dedicated static class (rather than a `Program.cs`-local variable, which is what
   this started as) is what actually makes it "one place" — the invalid-input `Attribute`s live in
   other files and need to reference the same value, not just the two scheme overrides in
   `Program.cs`.
   Fallback for non-truecolor terminals: confirmed via
   `new Color(32, 32, 32).GetClosestNamedColor16()` against the installed Terminal.Gui 2.4.17
   package that `(32, 32, 32)` resolves to `ColorName16.Black`, not `DarkGray` — i.e. on a terminal
   without truecolor support this degrades to the same neutral used for the rest of the app,
   matching the "it should fall back to black" requirement. (Note: this confirms the *palette
   distance* relationship, not that every driver path actually calls this exact method when
   downgrading — no lower-truecolor-capability driver was exercised in this environment to verify
   end-to-end.)
6. **Route every hardcoded `ColorName16.DarkGray` background through `Theme.EditableBackground`
   too — not just the two scheme overrides.** Live verification (decoding actual SGR truecolor
   escape codes from a real terminal, not just reading code) surfaced two gaps the initial
   implementation missed:
   - `PublishView.InvalidSubject`, `HeaderDialog.InvalidHeaderAttribute`, and
     `PatternDialog.InvalidPatternAttribute` each independently hardcoded
     `new(ColorName16.Red, ColorName16.DarkGray)` — invisible to the `"Base"`/`"Dialog"` scheme
     overrides entirely, so a field that's invalid by default (e.g. an empty required `Subject`)
     kept showing the old `DarkGray` regardless of the theme change. Fixed by referencing
     `Theme.EditableBackground` in all three.
   - `EditFrame.OnDrawingContent` only ever painted the 1-column LM/LP/RM borders and the TOP/BOT
     rows — never the child's own content rectangle. A child that doesn't paint its full bounds
     (an empty `TextField` only paints under actual characters) left that gap showing through to
     the root Toplevel's black background. Fixed by extending the LP fill across the full content
     width (one `FillRect` over columns `1..width-1`) instead of just column 1.
     Gotcha hit while fixing this: filling the gap with a space character and a custom
     `Attribute(inner, inner)` silently had no visible effect — a plain space glyph doesn't
     reliably carry a distinct background through Terminal.Gui's cell buffer/redraw diffing in
     this version. Using the same solid block glyph (`Ful`, `'█'`) already used for LP/RM, with
     `Attribute(inner, outer)` (inner as *foreground* ink), worked and is what's now in place —
     confirmed by checking the raw SGR codes, not just visually.
   - `ListEditorView<T>.Background`'s setter updated `_listView`'s scheme but not the
     `_emptyHintLabel` overlay, which fully covers the list while empty and was hardcoded to
     `GetScheme().Disabled` regardless of `Background`. Fixed so the hint label's background
     follows `Background` too (keeping its dim `Disabled` foreground) — this is what
     `HeaderEditorView`'s empty "No headers…" state needed to actually show the tunable color.
   - `PatternDialog`/`HeaderDialog` used a bare `TextField`, never wrapped in `EditFrame` the way
     `PublishView`'s fields are. A bare `TextField` only paints under its own characters, so most
     of its width (all of it, when empty/invalid, since there's nothing to paint at all) fell
     through to the surrounding `Dialog`'s own `Normal` gray/black — a mismatch between valid state
     (text visibly on the editor-background color) and invalid/empty state (uniformly the dialog's
     background instead), independent of whichever color the invalid `Attribute` used. Fixed by
     wrapping both fields in `EditFrame`, exactly like `PublishView`.

7. **`new Scheme(Attribute)`'s single-value constructor doesn't propagate the given background to
   the `Editable` role — confirmed by direct API probing against the installed Terminal.Gui
   package.** Given `new Attribute(Red, #202020)`, the resulting `Scheme.Editable` came back as
   `(Red, Black)` — a *different* background than `Scheme.Normal` (`Red, #202020`, correct) and
   `Scheme.Focus` (an inverted `#202020, Red`). Since `TextField` paints via `VisualRole.Editable`,
   this meant the invalid-state `new Scheme(InvalidXAttribute)` calls in `PublishView`/
   `HeaderDialog`/`PatternDialog` gave the field's own content (most visibly, the cursor cell when
   focused on an otherwise-empty field) a **black** background instead of
   `Theme.EditableBackground`, even though the `Attribute` itself was already correct — this is
   what the user was seeing as "still black when Subject has focus." The `EditFrame` border was
   never the problem here; it was already correctly using `Theme.EditableBackground` throughout
   (Decisions 4 and 6's `EditFrame`/`ListEditorView` fixes stand as-is).
   Fix: explicitly re-set `Editable = InvalidXAttribute` on the constructed `Scheme` after the
   single-value constructor runs (`Scheme` properties are settable), in all three files. Also
   removed the `InnerBackgroundOverride` assignments from all three `UpdateValidity()` methods —
   per explicit user direction, invalid state must only ever change the foreground; background is
   never touched, so there is nothing for `InnerBackgroundOverride` to do here (it remains a
   legitimate general-purpose escape hatch on `EditFrame` for other callers, per its own doc
   comment - just not needed by validity handling).

## Risks / Trade-offs

- **Explicit `Black` background may look different from a user's current terminal background
  (e.g. if they run with a transparent or non-black profile) → intentional**: this is exactly the
  behavior change requested — a consistent, app-defined background instead of terminal-dependent
  rendering. Documented here so it isn't mistaken for a regression.
- **`EditableBackground` (32,32,32) is a subtle step up from `Black` (0,0,0)**, subtler than the
  originally-tried `ColorName16.DarkGray` (118,118,118) → deliberate: the brighter `DarkGray` made
  field text harder to read and looked "shiny"; `(32,32,32)` was chosen specifically to keep
  contrast low while still being visibly distinct from the background. Revisit via the single
  `editableBackground` constant if it proves too subtle in practice.
- **Not verified against an actual non-truecolor/legacy terminal** — the fallback-to-`Black`
  claim rests on `Color.GetClosestNamedColor16()`'s palette-distance calculation, not on
  observing a real downgraded render. Low risk given the confirmed distance, but flagged as
  unverified end-to-end.
- **Overriding the shared `"Base"` scheme affects every current and future view that doesn't set
  its own `SchemeName`/`Scheme`** → this is the intended blast radius (that's the point of a
  central theme), but any future view that wants to opt out needs an explicit `SetScheme`/
  `SchemeName` override, same as today's invalid-state overrides already do.
- **`"Dialog"`'s `Editable` role becomes `DarkGray`, not `Black`, even though its current color
  (`#054D7A`) reads as "dark blue"** → deliberate deviation from a strict darkness-based mapping,
  so dialog input fields stay visually distinguishable from the dialog body (same reasoning as
  the rest of the app's `Normal`/`Editable` split). Flagged here in case it doesn't match
  expectations once seen live.

## Migration Plan

Additive, in-process only — no data/config migration. Rolling back is deleting the
`SchemeManager.AddScheme("Base", ...)` / `SchemeManager.AddScheme("Dialog", ...)` calls in
`Program.cs`, which reverts to Terminal.Gui's stock `"Base"`/`"Dialog"` schemes.
