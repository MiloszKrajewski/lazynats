## Why

Stress-testing the live feed under heavy load (via a throwaway load generator) turned up a real
bug: when the feed's 10,000-message ring buffer evicts old messages while the user is scrolled
away from the bottom, the highlighted row's numeric index never moves, so the *content* under a
stationary cursor silently changes to whatever slides into that slot - confirmed empirically by
selecting a specific message and watching the "Selected" detail dialog report a different message
after enough rollover, with no keys pressed. Two related rough edges compound this: `Clear` (`C`)
resets the message list but leaves the highlight sitting at a stale index, and there's no way to
explicitly stay pinned to "the newest message right now" without being dragged forward the instant
another message arrives - "following" is currently just inferred from whether the selection happens
to sit on the last row, which offers no way to intentionally pause there.

## What Changes

- Rollover (front-eviction once the buffer exceeds its cap) now shifts a not-following selection
  down by one per eviction, keeping it on the same message instead of a stale index. Once the
  selected message itself is the one evicted, selection lands on whatever is newly at index 0 -
  there is nothing else left to track.
- `Clear` (`C`) now also resets the list selection, so no highlight is left rendered at a stale
  index once the list is empty.
- The feed's follow state becomes an explicit, persisted flag instead of being derived from
  "is the selection on the last row": any manual navigation (arrow keys, Home/End, etc.) always
  pauses following, regardless of where it lands - including landing on the last row - so the
  feed can be deliberately parked on the newest message without being pulled forward by later
  arrivals. `Space` is the only way to resume following; pressing it while paused jumps to the
  true newest message and resumes, and pressing it while already following pauses in place
  without moving the selection.
- The feed's list gains a vertical scrollbar, so position within the (up to 10,000-message) buffer
  is visible while paused and browsing.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: adds requirements for identity-preserving selection across ring-buffer rollover,
  `Clear` resetting selection, an explicit follow/pause state with `Space` as the sole way to
  resume, and a vertical scrollbar on the feed list.

## Impact

- `src/lazynats/LiveFeed/LiveUpdatesView.cs` - primary implementation: rollover selection
  adjustment, `Clear`, follow/pause state machine, `Space` key binding, scrollbar.
- Terminal.Gui v2 `ListView` APIs: `SelectedItem`, `ValueChanged`, `ViewportSettings`
  (`ViewportSettingsFlags.HasVerticalScrollBar`). The follow/pause state machine needs a
  reentrancy guard so this view's own programmatic `SelectedItem`/`MoveEnd()` writes aren't
  misread as user-initiated navigation (to be confirmed empirically during implementation,
  mirroring how the rollover fix itself was verified against the running app rather than assumed
  from the framework's docs).
- No change to the feed pipeline itself (envelope wrapping, channel merge, dedup, batching) or to
  `MainWindow.cs` - the deferred status/position indicator (a follow-up ticket) is the only piece
  that would touch the latter.
