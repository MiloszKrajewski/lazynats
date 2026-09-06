## Context

`EditFrame`'s left-edge accent (`TL`/`BL`/`LM`, see `doc/glyphs.md`) currently uses a single
`EdgeAccent` color regardless of the wrapped child's focus state (default white, `DefaultEdgeAccent`
in `EditFrame.cs`). `color-theme` intentionally keeps the inner editable background constant
across focus states, so today an `EditFrame`-wrapped field has no focus indicator beyond a
`TextField`'s own blinking cursor — nothing at all for a non-`TextField` child (e.g. a
`ListEditorView<T>`-wrapped frame, or a `DropDownList<T>` whose own focus highlight is an inverted
bar inside the frame, not on the frame itself). No caller currently sets `EdgeAccent` (confirmed by
grep), so this is purely an `EditFrame`/`Theme` change with no call-site edits required.

## Goals / Non-Goals

**Goals:**
- Give every `EditFrame`-wrapped field a visible, focus-driven cue on its own accent line, without
  touching the inner/editable background (preserving `color-theme`'s "background doesn't change
  with focus" guarantee).
- Keep the existing `InnerBackgroundNormal`/`InnerBackgroundFocused`/override shape so the new
  property reads as the same pattern applied to the accent instead of a new mechanism.
- Make the new focused color the default so existing call sites pick it up with zero edits.

**Non-Goals:**
- Changing `InnerBackgroundNormal`/`InnerBackgroundFocused` or any other part of `color-theme`.
- Adding focus-state coloring anywhere outside `EditFrame`'s own left-edge accent.

## Decisions

- **New property `EdgeAccentFocused`, existing `EdgeAccent` keeps meaning "normal/unfocused"**:
  mirrors `InnerBackgroundNormal`/`InnerBackgroundFocused` exactly — `OnDrawingContent` picks
  `EdgeAccentFocused` when `_child.HasFocus`, else `EdgeAccent` (each falling back to its own
  default when unset), rather than folding focus-awareness into a single property with an enum
  flag or renaming `EdgeAccent` to `EdgeAccentNormal`. Alternative considered: rename `EdgeAccent`
  → `EdgeAccentNormal` for symmetry with `InnerBackgroundNormal` — rejected because it's a
  same-behavior rename with no callers to migrate, just churn.
- **Default focused color is `ColorName16.BrightBlue`, declared once in `Theme.cs`**: consistent
  with every other tunable color in the file (`SubjectColor`, `ShortcutKeyColor`, ...), and
  distinct from `Theme.SubjectColor` (`Cyan`) so the two never read as the same accent.
  `EdgeAccentFocused` defaults to `Theme`'s new constant the same way `EdgeAccent` already
  defaults to `DefaultEdgeAccent` (white) when unset — nullable property, non-null default
  resolved in `OnDrawingContent`.
- **No call-site changes**: since nothing sets `EdgeAccent` today, changing what "unset" resolves
  to for the focused state is the entire rollout. A caller that wants the old (no focus-distinction)
  look can still set `EdgeAccentFocused` explicitly to match `EdgeAccent`.

## Risks / Trade-offs

- [Risk] `BrightBlue` reads too close to `Cyan` on some terminal palettes → Mitigation: both are
  named, distinct entries in the standard 16-color ANSI palette (blue-hued vs. blue-green-hued);
  no code change can fix a terminal that remaps its own palette, same caveat as every other
  `ColorName16` constant already in `Theme.cs`.
- [Risk] A future caller wraps a child that itself uses blue as a semantic color, causing a clash
  with the new focused accent → Mitigation: none needed yet — no current usage does this; revisit
  if it comes up.
