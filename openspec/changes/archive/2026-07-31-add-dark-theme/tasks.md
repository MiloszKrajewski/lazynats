## 1. Base theme override

- [x] 1.1 In `Program.cs`, after `Application.Create()` and before `Run<MainWindow>()`, clone the
      existing `"Base"` scheme (`SchemeManager.GetScheme("Base")`) and register an overridden copy
      via `SchemeManager.AddScheme("Base", ...)` with:
      - `Normal` = `Attribute(ColorName16.Gray, ColorName16.Black)`
      - `Editable` = `Attribute(ColorName16.White, Theme.EditableBackground)`
      - all other roles left untouched from the cloned scheme

## 2. Dialog theme override

- [x] 2.1 In the same `Program.cs` location, clone the existing `"Dialog"` scheme
      (`SchemeManager.GetScheme("Dialog")`) and register an overridden copy via
      `SchemeManager.AddScheme("Dialog", ...)` with:
      - `Normal` = `Attribute(ColorName16.Gray, ColorName16.Black)`
      - `Focus` = `Attribute(ColorName16.Black, ColorName16.White)`
      - `Editable` = `Attribute(ColorName16.White, Theme.EditableBackground)`
      - all other roles left untouched from the cloned scheme

## 3. Editor-background tuning

- [x] 3.1 Replace `ColorName16.DarkGray` with a single named `Theme.EditableBackground` constant
      (`Color(32, 32, 32)` in `src/lazynats/Theme.cs`), used by the `"Base"`/`"Dialog"` overrides
      in `Program.cs`, so the editor-control color has one place to tweak.
- [x] 3.2 Confirm `new Color(32, 32, 32).GetClosestNamedColor16()` resolves to `ColorName16.Black`
      (not `DarkGray`), satisfying the "falls back to black without truecolor" requirement.

## 4. Fix stale/hardcoded consumers and gaps found during live verification

- [x] 4.1 `PublishView.cs`'s `InvalidSubject`, `HeaderDialog.cs`'s `InvalidHeaderAttribute`, and
      `PatternDialog.cs`'s `InvalidPatternAttribute` all hardcoded `ColorName16.DarkGray`
      independently of the theme - replace with `Theme.EditableBackground` in all three so the
      invalid-input highlight's background tracks the same tunable constant.
- [x] 4.2 `EditFrame.OnDrawingContent` only painted the LM/LP/RM border columns and TOP/BOT rows,
      never the child's own content rectangle - an empty child (e.g. a blank `TextField`, which
      only paints under actual text) left that area showing through to the root Toplevel's black
      background instead of the inner color. Extend the LP fill across the full width (columns
      1..width-1) as one solid-glyph fill so the child always has a correctly-colored backdrop
      regardless of what it draws on top.
- [x] 4.3 `ListEditorView<T>.Background`'s setter only updated `_listView`'s scheme, not the
      `_emptyHintLabel` overlay that fully covers the list while empty (hardcoded to
      `GetScheme().Disabled`, ignoring `Background` entirely) - update the hint label's scheme too
      (keeping its dim foreground, swapping in the chosen background) so `HeaderEditorView`'s list
      area actually shows the editor-background color while empty, not black.
- [x] 4.4 `PatternDialog`/`HeaderDialog` used a bare `TextField` (not wrapped in `EditFrame`), so
      only the actual typed characters were colored - the rest of the field's width (all of it,
      when empty/invalid) fell through to the surrounding `Dialog`'s own `Normal` gray/black,
      regardless of validity. Wrap both dialogs' fields in `EditFrame`, mirroring `PublishView`'s
      Subject/Headers/Payload fields.
- [x] 4.5 `new Scheme(Attribute)`'s single-value constructor derives `Editable` independently of
      the given `Attribute` and silently defaults its background to `Black` (confirmed via direct
      API probing against the installed Terminal.Gui package) - so
      `PublishView`/`HeaderDialog`/`PatternDialog`'s `new Scheme(InvalidXAttribute)` calls for the
      invalid state were giving the focused/empty field's cursor cell a **black** background, not
      `Theme.EditableBackground`, even though the `Attribute` itself was already correct. Fixed by
      explicitly re-setting `Editable = InvalidXAttribute` on the constructed `Scheme` in all
      three, and removed the now-redundant `InnerBackgroundOverride` assignments from
      `UpdateValidity()` in all three (background must never vary with validity - only the
      `TextField`'s own foreground does).

## 5. Verification

- [x] 5.1 Run the app (`dotnet run --project src/lazynats`) and confirm the main window background
      renders black and subject/header/payload edit frames in `PublishView` render the tuned
      editor-background color with no black gaps, in both focused and unfocused states.
- [x] 5.2 Open `PatternDialog` and `HeaderDialog` and confirm they render black/gray (no blue) and
      their text fields render the editor-background color across their full width - not just
      under typed text - distinguishable from the dialog body, whether valid or invalid.
- [x] 5.3 Confirm existing invalid-input highlighting (`PatternDialog`, `HeaderDialog`,
      `PublishView`'s subject field) still overrides correctly and now uses the tunable
      editor-background color instead of the old hardcoded `DarkGray`, matching the valid-state
      background exactly (only the foreground/red changes, not the background).
- [x] 5.4 Confirm focus-highlight behavior for non-editable controls (e.g. list selection,
      buttons, dialog buttons) is unchanged in shape (still a reverse-video style highlight), just
      no longer blue-tinted.
- [x] 5.5 Confirm `HeaderEditorView`'s list area (in `PublishView`'s Headers band) shows the
      editor-background color while empty, not black.
