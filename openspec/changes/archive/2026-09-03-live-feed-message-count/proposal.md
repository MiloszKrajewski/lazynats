## Why

The Live Feed has no indication of how many messages are currently buffered. A user watching a
busy subject has no cheap way to tell how much has accumulated (e.g. whether it's near the
10,000-message eviction cap) without counting rows by hand. The border of the "Live Feed" frame
is otherwise idle screen real estate immediately above the feed, and exploration confirmed a
technically clean way to render live status text there without fighting `Border`'s internal
`LineCanvas` painting.

## What Changes

- Render the live feed's current status as a small status readout positioned on the "Live Feed"
  `FrameView`'s bottom border row, right corner, updated live as messages arrive/are evicted, when
  `Clear` empties the feed, and when follow/pause state or the paused selection changes.
- The readout has two forms depending on the feed's follow state:
  - **Following** (auto-scrolling to the newest message): the total buffered count plus a down
    arrow, e.g. ` 42 ↓ ` - "following down" as new messages arrive.
  - **Sticky** (paused - the user has moved the selection off the newest message, so it stays put
    while the feed keeps arriving underneath it): the selected message's 1-based position out of
    the total, plus a filled circle marking the stopped/pinned state, e.g. ` 7/42 ● `.
  - Both forms are padded with a leading and trailing space for breathing room against the border
    line.
  - The entire status text is colored: green while following, yellow while sticky. A "completely
    inactive" red tier is anticipated for a future feed state but has no corresponding behavior
    to attach to yet.
- Implementation shape (settled during exploration, not renegotiated here): a small,
  non-focusable `Label` added as a **sibling** of `feedFrame` under `MainWindow` - not a child of
  `feedFrame.Border` and not inside `LiveUpdatesView`'s own content - positioned with ordinary
  `Pos`/`Dim` layout relative to `feedFrame` (bottom row, right-anchored) so it visually sits on
  the border line. Sibling placement means the label's own draw pass runs after `feedFrame`'s full
  draw pass (including the border's `LineCanvas` paint) completes, so no `LineCanvas.Exclude`
  bookkeeping is needed the way `Border`'s own title text requires internally.
- The label SHALL also be explicitly invalidated (`SetNeedsDraw()`) alongside any trigger that
  invalidates `feedFrame`'s own border (resize, `feedFrame` focus change) since Terminal.Gui's
  dirty-tracking does not cascade sideways between siblings, and the border adornment can redraw
  independently of the status changing.
- Out of scope for this change: further visual styling beyond the two forms above and their
  indicator coloring (e.g. additional fields, a red "inactive" tier with no state to represent
  it yet).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: adds a requirement that the feed's current buffered count and follow/sticky state
  are rendered as live status text on the "Live Feed" frame's border, kept in sync with the
  feed's own buffer (arrivals, eviction, `Clear`) and its follow/pause state.

## Impact

- `MainWindow.cs`: construct and position the new status label alongside `feedFrame`; wire it to
  `feedFrame`'s resize/focus-change invalidation.
- `LiveUpdatesView.cs` (`LiveFeed/`): expose the current buffered count, follow state, and (when
  paused) the selected message's position, for the label to consume - it does not render this
  status itself, per the existing "No In-View Header" requirement in the `live-feed` spec, which
  keeps framing owned by the host container.
- `openspec/specs/live-feed/spec.md`: new requirement + scenarios for the border status readout.
