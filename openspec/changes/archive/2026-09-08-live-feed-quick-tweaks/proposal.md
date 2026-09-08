## Why

Two small live-feed/message-view rough edges have been sitting unaddressed: the Live Feed
shortcut doesn't fit the app's numbered-tab pattern, and the Message Detail dialog's Headers
section is visually indistinguishable from Subject/Payload even though it benefits from the same
at-a-glance color cue Subject already gets.

## What Changes

- Rebind the global "focus Live Feed" shortcut from `Alt+M` to `Alt+0`, so it reads as slot 0 in
  the same `Alt+<digit>` sequence as the `Alt+1`..`Alt+5` management tabs.
- Give the Headers section of `MessageDetailDialog` a green foreground color by reusing the
  existing `Theme.HeaderColor` (CSS LimeGreen) - the same color the live feed row and
  `ValueDetailDialog`'s metadata section already use for "this is a message's headers" - so
  header lines are visually distinct from plain payload/subject text and consistent with every
  other place headers are shown.
- Retitle the Live Feed frame from `" Live Feed "` to `" 0:Live Feed "`, so it reads as slot 0 in
  the same `N:Title` pattern the `Alt+1`..`Alt+5` management tabs already use for their own titles
  (`" 1:Subscribe "`, `" 2:Streams "`, ...), matching the `Alt+0` binding above.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: the "Alt+M Focuses the Live Feed" requirement changes its bound key to `Alt+0`
  (the shortcut itself, and both of its scenarios, are otherwise unchanged); the "No In-View
  Header" requirement's scenario updates its host-frame title reference from "Live Feed" to
  "0:Live Feed".

(The Headers-color change is not listed here: `message-detail-dialog`'s spec covers dialog
content, not section coloring - it doesn't even spec Subject's existing cyan - so this stays an
implementation-level presentation detail, not a spec-level requirement change.)

## Impact

- `src/lazynats/MainWindow.cs`: the `topLevelShortcuts` entry for Live Feed changes its bound
  `Key` from `Key.M.WithAlt` to `Key.D0.WithAlt`.
- `src/lazynats/LiveFeed/MessageDetailDialog.cs`: the headers `EditFrame`'s content view gets a
  `SetScheme` override using `Theme.HeaderColor`, following the same pattern already used for the
  subject view (`Theme.SubjectColor`) and for `ValueDetailDialog`'s metadata section.
- `src/lazynats/Theme.cs`: unchanged - reuses the existing `HeaderColor` constant.
- `src/lazynats/MainWindow.cs`: the Live Feed `FrameView`'s `Title` changes from `" Live Feed "`
  to `" 0:Live Feed "`.
