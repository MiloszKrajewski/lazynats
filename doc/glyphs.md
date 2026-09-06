# Glyph reference

Naming reference for the half-block/quadrant glyphs used to draw `EditFrame`'s padded border
(see `openspec/changes/add-edit-frame/`). Two separate naming systems — a glyph name is a shape,
a position name is a frame slot; which glyph currently occupies which slot is a mapping, not an
identity.

## Glyph names

The shape itself, independent of where it's placed. All are Unicode block-element characters
whose "ink" fills only part of the character cell.

| Codename | Glyph | Unicode name | Codepoint |
|---|---|---|---|
| `HBD` | `▄` | LOWER HALF BLOCK (half-block, down) | U+2584 |
| `HBU` | `▀` | UPPER HALF BLOCK (half-block, up) | U+2580 |
| `HBR` | `▐` | RIGHT HALF BLOCK (half-block, right) | U+2590 |
| `HBL` | `▌` | LEFT HALF BLOCK (half-block, left) — reserved, unused so far | U+258C |
| `FUL` | `█` | FULL BLOCK | U+2588 |
| `QLR` | `▗` | QUADRANT LOWER RIGHT | U+2597 |
| `QUR` | `▝` | QUADRANT UPPER RIGHT | U+259D |
| `QLL` | `▖` | QUADRANT LOWER LEFT — reserved, unused so far | U+2596 |
| `QUL` | `▘` | QUADRANT UPPER LEFT — reserved, unused so far | U+2598 |

Quadrant glyphs (`Qxx`) have historically had spottier terminal-font support than half-blocks —
verify rendering in the actual target terminal/font before relying on them.

## Position names

Frame slots — where a glyph sits, independent of what currently fills it:

- **TL** — top-left corner cell
- **BL** — bottom-left corner cell
- **TOP** — top rule row, repeats across the full width (including the top-right corner cell —
  there is no distinct top-right glyph)
- **BOT** — bottom rule row, repeats across the full width (including the bottom-right corner
  cell — no distinct bottom-right glyph)
- **LM** — left margin column (outer), repeats down every content row
- **LP** — left padding column (inner, between LM and the child's own content), repeats down
  every content row
- **RM** — right margin column, repeats down every content row — one column only, no outer accent
  tick; asymmetric with the left side by confirmed design choice

```
+----------------------------------+
|  TL  TOP TOP TOP TOP TOP TOP ... |
|  LM  LP  content . . . . .   RM  |
|  BL  BOT BOT BOT BOT BOT BOT ... |
+----------------------------------+
```

## Current mapping (EditFrame)

| Position | Glyph |
|---|---|
| TL | QLR |
| BL | QUR |
| TOP | HBD |
| BOT | HBU |
| LM | HBR |
| LP | FUL |
| RM | FUL |

## Colors

- **OBC** — outer background color (the ambient color behind the frame; `EditFrame`'s
  `OuterBackground`).
- **IBC** — inner background color (matching the wrapped control's own background; whichever of
  `InnerBackgroundOverride ?? (focused ? InnerBackgroundFocused : InnerBackgroundNormal)` is
  currently active — resolved fresh on every draw, not a fixed single color).
- **EAC** — edge accent color (`EditFrame`'s `EdgeAccent`, `Color?` defaulting to white
  `ColorName16.White` when unset) — a deliberate accent distinct from IBC, used only at the
  frame's outermost left edge.

Every glyph in the frame uses **background = OBC** for its unfilled portion. For the filled
("ink") portion:
- **TOP, BOT, LP, RM** ink with **IBC** — the color meant to read as "matching the child."
- **TL, BL, LM** ink with **EAC** instead — the outermost left edge is a deliberate accent line,
  not a continuation of the child's own color.

The glyphs paint their visible ink via foreground, not background, so whichever color applies
(IBC or EAC) has to go in the foreground slot of the `Attribute`, not the background. `RM`/`LP`
(`FUL`, fully opaque) are the cases where OBC-as-background never actually shows through, since
the cell is 100% covered by ink; keep the construction consistent there anyway rather than
special-casing it.
