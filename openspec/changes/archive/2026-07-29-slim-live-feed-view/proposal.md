## Why

`LiveUpdatesView` draws its own divider line and a "Live Updates" heading at the top of its
content, but it is always hosted inside `MainWindow`'s `FrameView`, whose `Title = " Live Feed "`
already renders as the frame's border caption. The result is a redundant header: the same
information shown twice, and two rows of vertical space spent on it in an already space-constrained
25%-of-screen pane.

## What Changes

- Remove the in-view `Line` divider and `Label` heading from `LiveUpdatesView`.
- The feed `ListView` starts at `Y = 0` and fills the view, reclaiming the two rows.
- The view relies entirely on its host `FrameView`'s title/border for framing — no in-view
  restatement of the title.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: adds a presentation requirement that the feed view has no in-view heading/divider
  of its own and depends on its host container for a title.

## Impact

- `src/lazynats/LiveUpdatesView.cs`: remove `divider`/`heading`, shift `ListView` to `Y = 0`.
- No change to `MainWindow.cs`'s `FrameView` (already titled `" Live Feed "`).
