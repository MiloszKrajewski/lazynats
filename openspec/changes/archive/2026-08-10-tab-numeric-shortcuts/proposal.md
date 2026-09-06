## Why

Terminal.Gui derives a button's own Alt+letter hotkey automatically from its `_Mnemonic` text
(e.g. `PublishTab`'s `_Send` button responds to Alt+S), and the global tab-switch shortcuts draw
from that same Alt+letter keyspace. That's not a one-off collision to dodge per tab (as
`tab-navigation` already does today by putting Subscribe on Alt+B instead of Alt+S to leave room
for a future Streams tab) — it's a structural clash between two independent producers of
Alt+letter bindings that will keep recurring as more tabs and per-tab buttons are added. Moving
tab-switching to a separate keyspace (Alt+digit) removes the whole collision class permanently
instead of relitigating it one letter at a time.

## What Changes

- Tab-switch shortcuts change from per-tab Alt+letter (Alt+B, Alt+P, ...) to Alt+1..Alt+9,
  assigned by left-to-right tab order (Alt+1 = first tab, Alt+2 = second tab, ...). **BREAKING**:
  existing Alt+B (Subscribe) and Alt+P (Publish) bindings are rebound to Alt+1 and Alt+2.
- Tab titles gain a positional number prefix in tmux-style `N:Title` format (e.g. `1:Subscribe`,
  `2:Publish`) — no space around the colon — keeping the existing leading/trailing space padding
  convention (e.g. `" 1:Subscribe "`).
- `doc/UI.md`'s tab shortcut table is updated to show the Alt+N scheme and `N:Title` format in
  place of the current Alt+letter table.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `tab-navigation`: the "Alt+Letter Tab Switching" requirement is replaced by an Alt+digit
  scheme keyed to tab position, and the "Tab Titles" requirement is extended to specify the
  numbered `N:Title` title format.

## Impact

- `src/lazynats/MainWindow.cs`: the `Shortcut` definitions for tab switching (currently
  `Key.B.WithAlt`, `Key.P.WithAlt`) and the tab `Title` strings (`" Subscribe "`, `" Publish "`).
- `doc/UI.md`: the tab shortcut table and its accompanying notes (e.g. the "Subscribe uses Alt+B
  rather than Alt+S" explanation, which no longer applies once tabs use Alt+digit).
- No other tabs exist yet (Streams/Consumers/KV/OBJ are still reserved, not built), so no other
  construction sites are affected today.
