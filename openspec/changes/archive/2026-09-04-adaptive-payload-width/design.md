## Context

`MessageDetailDialog` wraps its payload `Label` in an `EditFrame` sized to `Dim.Fill()` inside the
dialog's own `Padding.Thickness`. The dialog's width is itself `Dim.Func(_ => Math.Min(132,
app.Screen.Width - 4))`, so at construction time the actual pixel/column width available to the
payload `Label` is already known and computable:

```
labelWidth = dialogWidth - 2 (Padding.Thickness left+right)
                         - 2 (EditFrame's child.X=2 left margin)
                         - 1 (EditFrame's child Width=Dim.Fill(1) right margin)
           = dialogWidth - 5
```

`PayloadPresentation.Render` currently ignores this entirely: `Hex` hardcodes 16 bytes/row
(`RenderHex`), and `Base64` is `Convert.ToBase64String(data)` with no line breaks at all. Because
the payload `Label` has `TextFormatter.WordWrap = false` and only a vertical scrollbar
(`BindScrollKeys` binds Up/Down/PageUp/PageDown only, never left/right), a `Base64`-rendered
payload wider than `labelWidth` is already unreachable today - not a hypothetical edge case, an
existing gap for any moderately-sized binary payload switched to `Base64` presentation.

## Goals / Non-Goals

**Goals:**
- Make `Hex` bytes/row use the width actually available, instead of a fixed 16, snapped to a small
  set of conventional values.
- Make `Base64` wrap at the width actually available, fixing the existing horizontal-overflow gap.
- Make `Text` wrap at the width actually available - its wire bytes are frequently one unbroken
  line (e.g. a minified JSON payload viewed as `Text` rather than `Json`) with no indentation of
  its own to keep it inside the visible width, unlike `Json`.
- Keep the computation a plain, testable function of a width - no coupling to `View`/layout types
  inside `PayloadPresentation` itself, which stays a display-formatting-only static class.

**Non-Goals:**
- Live-reactive resizing while the dialog is open. `Json` already ignores a live terminal resize
  once rendered - `Hex`/`Base64`/`Text` width is computed once at construction (and once per
  presentation switch, from that same stored width), matching that existing lifecycle rather than
  introducing a new one. This is also consistent with `design.md`'s prior decision
  (payload-presentation change) to fix the payload frame's *height* at construction from the
  default type's line count, so the dialog doesn't resize itself out from under the user
  mid-session.
- A user-facing control to manually pick row/line width - the value is derived, not chosen.
- Changing `Json` rendering in any way, or reformatting `Text`'s content beyond wrapping it -
  `Text` still shows the bytes' decoded UTF-8 verbatim, just with line breaks inserted at the
  available width.

## Decisions

### Decision 1: `Render` takes a width parameter; only `Json` ignores it
`PayloadPresentation.Render(byte[] data, PayloadType type)` becomes
`Render(byte[] data, PayloadType type, int width)`. Only `Json` ignores `width` entirely (it
already doesn't wrap, and its own indentation already breaks most payloads into reasonably-sized
lines). This is a single, uniform entry point rather than separate methods per presentation, since
`MessageDetailDialog` calls `Render` generically from both the constructor and
`OnPresentationChanged` without branching on `type` itself. `PayloadPresentation.Render`'s only
caller today is `MessageDetailDialog` (both call sites), so this is a safe, low-blast-radius
signature change despite being source-breaking.

### Decision 2: width is computed once in `MessageDetailDialog`, stored, reused
`MessageDetailDialog` computes `labelWidth` once (from the payload `EditFrame`'s resolved
`Viewport.Width` after initial layout, following the same `child.X=2`/`Width=Dim.Fill(1)` math
`EditFrame` itself already uses to lay out its child - not re-derived ad hoc from
`PreferredDialogWidth`/`TerminalWidthMargin`, since those are the dialog's own preferred/max width,
not necessarily its resolved width on a smaller terminal), converts it once into `hexBytesPerRow`
and `base64LineWidth`, and passes those into every `Render` call for the dialog's lifetime -
mirroring how `_payloadVisibleLines` is already "fixed at construction" per the existing
payload-presentation design.

The measured `Viewport.Width` is taken *before* `RefreshPayloadScrollState` (run once, at the end
of construction, from the actual rendered line count) ever turns the vertical scrollbar on - so it
doesn't yet reflect the column the scrollbar will claim once a payload turns out taller than
`MaxPayloadVisibleLines`. There's no non-circular way to know that in advance (whether the
scrollbar is needed depends on the rendered line count, which depends on this same width), so a
constant `ScrollbarWidth = 1` is subtracted unconditionally, at the point `labelWidth` is stored -
wasting a column on the (common) case where no scrollbar ends up shown, rather than overflowing
under one when it does. Found via manual testing: a tall `Hex`/`Base64` payload (more than
`MaxPayloadVisibleLines` rows) rendered its rightmost column(s) under the scrollbar before this
fix.

### Decision 3: `Hex` snaps down to a fixed candidate set, not a continuous value
Candidates: `8, 16, 24, 32, 48, 64`. A continuous bytes/row value would produce an oddly-specific
number (e.g. 27) that reads as arbitrary; snapping to values a reader of hex dumps already
recognizes (16 is `xxd`/`hexdump -C`'s own default; 8/32/64 are common alternates) keeps the output
looking deliberate. The set isn't pure powers-of-two (`24`, `48` are included per explicit
request) - snapping is implemented as "largest candidate `<= (width+1)/3`" over a sorted array,
not a bit-trick, so the non-power-of-two entries cost nothing extra. Clamped
`.NotLessThan(8).NotMoreThan(64)`.

### Decision 4: `Base64` floors to the nearest multiple of 4, not a fixed standard column count
Considered: fixed 76 (RFC 2045/MIME) or fixed 64 (RFC 1421/RFC 7468 PEM). Both are recognizable
conventions, but both can still overflow a narrow dialog or under-use a wide one - exactly the
problem being fixed. Flooring to the label's actual width (rounded down to a multiple of 4, since 4
encoded chars = 3 raw bytes, keeping line breaks on clean quantum boundaries) generalizes the same
"fit what you're given" approach as `Hex`, rather than picking a second fixed constant. Clamped
`.NotLessThan(24).NotMoreThan(144)` - floor matches `Hex`'s own floor (8 bytes/row = 23 chars,
rounded up to the nearest multiple of 4); the 144 cap is roughly the label width at the dialog's
own preferred maximum (`PreferredDialogWidth = 132`), so `Base64` doesn't run arbitrarily wide past
where the dialog itself would ever actually be.

### Decision 5: generic `NotLessThan`/`NotMoreThan` clamp extensions
Added as `Comparer<T>.Default`-based generic extensions (not `int`-only), alongside the existing
`Core/*Extensions.cs` files (`ApplicationExtensions.cs`, `AsyncExtensions.cs`, ...). Generic over
`T` costs nothing extra at the two `int` call sites here and is more broadly reusable than a
narrower `int`-only pair.

### Decision 6: `Text` wraps at a fixed character count, not word boundaries
`RenderText` first tried Terminal.Gui's own `TextFormatter.WordWrapText(line, width)` (called once
per existing line, splitting the decoded text on `\n` first, joining wrapped results back with
`\n` - still embedding a literal `\n` rather than enabling `TextFormatter.WordWrap` on the payload
`Label` itself, for the same "wrap once, at the width known at construction" reasons `Hex`/`Base64`
already establish). In manual testing against real JetStream advisory payloads (JSON, effectively
one giant "word" with no spaces except occasionally inside a string value), that produced ragged,
misleading output: `WordWrapText`'s long-word fallback hard-splits the giant run wherever it
happens to be mid-run, but only after first placing a short, mostly-empty line - e.g. a
`"name":"NATS .NET Client"` value split across three lines as `...\"name\":\"N` / `ATS .NET`
(mostly blank) / `Client\",...`, with the mid-word "N"/"ATS" split having nothing to do with any
actual word boundary. `RenderText` now uses the same plain, fixed-character-count chunking
`Hex`/`Base64` already use (factored into a shared `ChunkFixedWidth` helper) - every wrapped row
fills the full width consistently, which reads as deliberate rather than broken, even though it
can still split a real word arbitrarily. `PayloadPresentation` no longer references any
`Terminal.Gui` type as a result, reinforcing the "display-formatting-only, no coupling to
`View`/layout types" goal.

### Decision 7: `ScrollbarGap` reserves a visual gap on top of `ScrollbarWidth`
Reserving only `ScrollbarWidth` (Decision 2) stops content from running under the scrollbar, but
leaves it flush against that column with no breathing room - found, again, via manual testing:
Base64 output visibly touched the scrollbar glyph. A second constant, `ScrollbarGap = 1`, is
subtracted alongside `ScrollbarWidth` when `_payloadLabelWidth` is computed, purely for appearance
(the same "reserve unconditionally, waste a column when not strictly needed" reasoning as
`ScrollbarWidth` itself applies here too).

## Risks / Trade-offs

- **Stored width can go stale if the terminal is resized while the dialog is open** → Accepted
  per Non-Goals: matches existing `Json` behavior, which already doesn't re-wrap on resize.
  Visually this means rows/lines don't reflow to a new width until the dialog is reopened, same as
  today's fixed-16 `Hex` already doesn't reflow either.
- **Reserving `ScrollbarWidth` unconditionally wastes a column when the payload turns out short
  enough that no scrollbar appears** → Accepted: the alternative is a circular "reserve only if
  needed" computation (see Decision 2), and a wasted column is far less visible than content
  running under the scrollbar.
- **Snapping `Hex` down (rather than to nearest) can leave visible right-hand blank space at some
  widths** (e.g. exactly enough room for 47 bytes/row snaps down to 32) → Accepted: consistent
  with `Hex`'s own existing self-imposed constraint of always being a fixed value, and erring
  toward *not* overflowing outweighs a few unused columns.
- **Base64's 144 cap is a fixed number, same category of "picked constant" as the standards being
  avoided** → Mitigated: it's a cap derived from the dialog's own known preferred-width ceiling
  (`PreferredDialogWidth`), not an external convention, so it moves with the dialog if that
  constant ever changes, unlike RFC 2045/RFC 1421's fixed 76/64.

## Open Questions

None outstanding - width computation, snap/clamp values, and the clamp extensions' shape were all
settled during exploration prior to this proposal.
