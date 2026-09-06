## Context

`tab-navigation` currently binds each management tab to a hand-picked Alt+letter (Alt+B for
Subscribe, Alt+P for Publish), already working around one collision: Subscribe uses Alt+B instead
of its own initial specifically to leave Alt+S free for a future Streams tab. That workaround
doesn't generalize — Terminal.Gui buttons derive their own Alt+letter hotkey automatically from
an underscore-prefixed mnemonic (e.g. `PublishTab`'s `_Send` button responds to Alt+S), so the
global tab-switch keyspace and the per-tab local-button keyspace are the same keyspace. Every new
tab and every new mnemonic button is a fresh chance to collide, not just Streams vs. Send.

## Goals / Non-Goals

**Goals:**
- Eliminate the structural collision between global tab-switch shortcuts and per-tab button
  mnemonics, permanently rather than per-instance.
- Keep "switch tab from anywhere, including from inside another tab's content" behavior intact.
- Keep the shortcut discoverable at a glance from the tab's own title.

**Non-Goals:**
- Changing per-tab button mnemonics themselves (they keep using Terminal.Gui's normal
  underscore-prefix Alt+letter convention).
- Changing arrow-key header navigation (Left/Right/Up/Down) — unaffected by this change.
- Building the not-yet-implemented tabs (Streams, Consumers, KV, OBJ) — this change only touches
  the shortcut/title scheme, applied to whichever tabs exist today (Subscribe, Publish).

## Decisions

### Alt+digit (Alt+1..Alt+9) instead of function keys (F1..F9)
Frees the entire Alt+letter space for local button mnemonics with no further bookkeeping, and
avoids the laptop Fn-lock friction that function keys carry (the F-row defaults to
brightness/volume/media on most laptops, requiring an extra Fn press unless Fn-lock is toggled).
Alt+digit also matches a convention the app's target audience — terminal/IDE power users — likely
already has (e.g. JetBrains IDEs use Alt+1..Alt+9 for tool-window switching).

Alternative considered: F1..F9. Rejected primarily for the Fn-lock friction, plus common terminal
emulator/OS reservations on some F-keys (F1 as "help" by convention, F11 as fullscreen in most
terminal emulators including Windows Terminal).

Trade-off accepted: some Linux window managers (i3, GNOME workspace extensions) bind Alt+1..9 to
virtual-desktop switching and could intercept the key before the terminal sees it. Accepted for
now since the primary target is Windows and this is a per-user WM configuration concern, not a
hard blocker; revisit if Linux usage grows.

### Assignment is positional (left-to-right tab order), not per-letter
Alt+N maps to the Nth tab in display order (Alt+1 = first tab, Alt+2 = second, ...), rather than
hand-picking a free letter per tab. This removes the exact bookkeeping problem the current Alt+B
workaround exists to paper over — no need to reserve letters for tabs that don't exist yet.

Trade-off: loses the direct letter↔name mnemonic (Alt+S ↔ "Streams"); mitigated by showing the
number directly in the tab's own title, so the binding is visible wherever the tab is.

### Title format: tmux-style `N:Title`, no space around the colon
Chosen over bracket style (`[N] Title`). Rationale:
- Matches an existing terminal convention for "numbered switchable panel" (tmux's window list:
  `1:bash 2:vim*`), which the target audience already recognizes.
- This project's own `[X]` bracket notation (`doc/UI.md`'s `[P]ublish`) specifically marks a
  *letter* mnemonic; reusing it for a position number risks reading as if the number still works
  like a letter-hotkey, which it doesn't.
- More horizontal-space-efficient (`N:` adds 2 characters of header chrome vs. `[N] `'s 4), which
  matters once the tab strip holds 6-9 tabs sharing one row.

The existing leading/trailing space convention on bordered-container titles is unaffected: the
number goes inside that padding, e.g. `" 1:Subscribe "`.

## Risks / Trade-offs

- [Breaks existing Alt+B/Alt+P muscle memory] → Accepted as a **BREAKING** change; no material
  install base yet, and the new scheme is what future tabs need anyway.
- [Alt+1..9 may be intercepted by some Linux window managers] → Accepted risk; Windows is the
  primary target today.
- [Numeric prefix adds a small amount of visual noise to every tab title] → Two characters per
  title is small relative to typical tab name lengths, and it's the mechanism that keeps the
  shortcut discoverable without consulting the status bar.

## Migration Plan

Single-release change, no persisted state involved: update the `Shortcut` key bindings and tab
`Title` strings in `MainWindow.cs` together with `doc/UI.md`'s tab table in the same change. No
rollback complexity beyond reverting the commit.

## Open Questions

- Alt+0 is left unassigned (spare for a future 10th tab, or some other purpose) — not decided
  here, revisit when it's actually needed.
