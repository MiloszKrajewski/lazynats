## Context

`CreateKeyDialog` (`src/lazynats/Values/CreateKeyDialog.cs`) is the "New Key"/"Edit Key" modal.
Today its `WrapField` helper hardcodes `Width = 43` for both the Name and Value fields, and the
Value field's frame is given a fixed `Height = 10`. The Dialog itself has no explicit `Width`/
`Height`, so `Dialog`'s own `Dim.Auto` sizes it to exactly fit those fixed children - it never
grows even when the terminal is much larger.

`ValueDetailDialog` (the read-only viewer for the same KV entries) already solves the width half
of this problem: `Width = Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width -
TerminalWidthMargin))`, reading `IApplication.Screen` via `Services.Root.GetRequiredService<
IApplication>()`. `EditFrame.CreateReadOnly` (used throughout that dialog) already wraps its
content in a frame with `Width = Dim.Fill()`, so once the Dialog itself is responsive, its
read-only frames track it for free.

## Goals / Non-Goals

**Goals:**
- Name and Value fields' width tracks the terminal width, up to a sensible cap, the same way
  `ValueDetailDialog` already does.
- The Value field's height tracks the terminal height, up to a cap, so more of a long value is
  visible without scrolling on a large terminal.
- Small terminals still get a usable dialog - never shrink below today's fixed 43x10 floor for
  the Value field, and the dialog must still fit on-screen.

**Non-Goals:**
- No change to validation, edit/create semantics, keybindings, or `NewKeyOptions`.
- No change to `CreateBucketDialog` or any other dialog - `CreateKeyDialog` only.
- Not attempting word-wrap or reflow of existing content; `TextView`'s own behavior is unchanged.

## Decisions

- **Dialog width**: reuse `ValueDetailDialog`'s exact pattern - `Width = Dim.Func(_ =>
  Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin))`, with the same
  `TerminalWidthMargin = 4`. `PreferredDialogWidth` is set to `132`, matching
  `ValueDetailDialog`'s constant, so both KV dialogs read as the same "wide" size rather than two
  different cap values that need separately justifying.
  - *Alternative considered*: a smaller cap (e.g. 90) sized just for this dialog's two fields.
    Rejected - there's no functional reason for the create/edit dialog to cap narrower than the
    read-only viewer of the same data, and reusing the constant keeps the two dialogs visually
    consistent.

- **Field width**: change `WrapField`'s hardcoded `Width = 43` to `Width = Dim.Fill()`. Both
  fields are direct children of the Dialog's content area (inside `Padding.Thickness`), so
  `Dim.Fill()` already tracks the Dialog's own (now-responsive) width - this mirrors
  `EditFrame.CreateReadOnly`'s existing `Width = Dim.Fill()` and needs no new screen-reading code
  of its own.

- **Value field height**: computed once in the constructor from `app.Screen.Height`, clamped
  between the existing fixed value (`MinValueHeight = 10`, today's floor) and a new
  `MaxValueHeight = 30` cap, after subtracting a fixed `ReservedChromeRows` for everything else
  the dialog already draws (title bar, top padding, Name label + frame, Value label, button row,
  borders). `ReservedChromeRows = 14` covers: 1 top padding + 1 Key... actually Name label (1) +
  Name frame (3) + Value label (1) + button row incl. its own blank separator row (2) + dialog
  top/bottom border and title (2) + a few rows of margin so the dialog never touches the terminal
  edges (4). This mirrors `ValueDetailDialog`'s own "compute a frame height, then derive layout
  offsets from it" style, but keyed off `app.Screen.Height` instead of content-line-count since
  Value is an editable field, not a read-only block sized to its content.
  - *Alternative considered*: leave Value's height content-driven like
    `PayloadDetailSection.MaxPayloadVisibleLines` (cap at content's own line count). Rejected -
    Value is actively edited here (new text can be typed in), so sizing to the *current* content
    would keep the box small right up until the user types past it, then jump; a terminal-height-
    driven size gives a stable, predictable editing area from the moment the dialog opens.
  - *Alternative considered*: no cap (`MaxValueHeight` unbounded, i.e. always `screen.Height -
    ReservedChromeRows`). Rejected - on a very tall terminal this makes the Value box
    disproportionately large relative to the two-field dialog's actual content; 30 rows is
    already several times today's fixed 10 and enough for the vast majority of KV values.

- **Name field height**: left untouched (fixed `3`, i.e. one line of text plus its frame) - it's
  always exactly one line of input, so there's nothing to gain from growing it.

- **Dialog height**: left unset, as today - `Dialog`'s own `Dim.Auto` continues to size the
  dialog to fit the now-taller Value frame plus the fixed rows around it, same mechanism
  `ValueDetailDialog` already relies on for its own auto height.

## Risks / Trade-offs

- [Screen too small for even the floor size] → `MinValueHeight`/the width's `TerminalWidthMargin`
  subtraction match `ValueDetailDialog`'s existing floor behavior, which is already relied on in
  this codebase; an unusably tiny terminal is a pre-existing condition this change doesn't make
  worse.
- [Reserved-chrome-rows constant drifts out of sync with the dialog's actual fixed layout if a
  future change adds/removes a row above the Value field] → keep `ReservedChromeRows` and the Y
  offsets it's derived alongside next to each other in the constructor (same file, same method),
  the same locality `ValueDetailDialog` uses for its own offset math, so a future edit to one is
  visually adjacent to the other.
