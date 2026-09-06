## 1. Theme

- [x] 1.1 Add a new centrally-declared constant in `Theme.cs` for the focused edge accent color
      (`ColorName16.BrightBlue`), following the existing constant style/comment pattern.

## 2. EditFrame

- [x] 2.1 Add a nullable `EdgeAccentFocused` property to `EditFrame` (same shape as the existing
      `EdgeAccent` property: setter calls `SetNeedsDraw()`).
- [x] 2.2 In `OnDrawingContent`, select the accent color based on `_child.HasFocus`:
      `EdgeAccentFocused ?? Theme`'s new constant when focused, `EdgeAccent ?? DefaultEdgeAccent`
      (existing behavior) when not.
- [x] 2.3 Update the class-level doc comment / `Edge Accent Color` inline comments to describe the
      focus-driven pair instead of a single static accent.

## 3. Verification

- [x] 3.1 Run the app (`dotnet run --project src/lazynats`, or via tmux per `CLAUDE.md`) and tab
      through a dialog with multiple `EditFrame`-wrapped fields (e.g. `CreateStreamDialog` or
      `PublishDialog`) to confirm the focused field's left edge turns light blue and reverts to
      white when focus moves away, including for a non-`TextField` child (e.g. a
      `ListEditorView<T>`-wrapped frame or a `DropDownList<T>`-wrapped one).
- [x] 3.2 Confirm inner/editable background is unchanged (still constant across focus states) —
      only the left-edge accent should differ.
