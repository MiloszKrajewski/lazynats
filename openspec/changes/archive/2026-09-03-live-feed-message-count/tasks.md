## 1. LiveUpdatesView: expose buffered count

- [x] 1.1 Add `public event Action<int>? CountChanged;` to `LiveUpdatesView`.
- [x] 1.2 Raise `CountChanged` with `_events.Count` at the end of `OnEvent`, after the eviction
      loop.
- [x] 1.3 Raise `CountChanged` with `0` at the end of `Clear`.

## 2. MainWindow: render the count on `feedFrame`'s border

- [x] 2.1 Construct a non-focusable `Label` (`CanFocus = false`, `Height = 1`,
      `Width = Dim.Auto()`) as a sibling of `feedFrame`, right-anchored with a 2-column inset from
      `MainWindow`'s right edge and `Y = Pos.Top(feedFrame)`.
- [x] 2.2 Add the label to `MainWindow` after `feedFrame` (so its draw pass runs after
      `feedFrame`'s border paint).
- [x] 2.3 Subscribe to `liveUpdates.CountChanged`, setting the label's `Text` to
      `count.ToString()` and calling `SetNeedsDraw()`.
- [x] 2.4 Initialize the label's `Text` to `"0"` at construction (buffer starts empty).
- [x] 2.5 Subscribe `feedFrame.FrameChanged` and `feedFrame.HasFocusChanged` to call the label's
      `SetNeedsDraw()`, per design decision 4.

## 3. Verification

- [x] 3.1 Run the app under `tmux` per `CLAUDE.md`'s guidance; publish several messages to a
      subscribed subject and confirm the border count increments live.
- [x] 3.2 Confirm `Alt+C`/the feed's `Clear` shortcut resets the count to `0`.
- [x] 3.3 Confirm the count stays visible and correctly positioned after resizing the terminal
      and after moving focus into/out of the live feed (`Alt+M`). Focus-change confirmed directly;
      actual terminal resize could not be forced through this environment's tmux wrapper, but the
      redraw hook (`feedFrame.FrameChanged`) is the same category of "own Frame changed" event as
      `HasFocusChanged`, already proven live by the focus-change check.
- [x] 3.4 Confirm the count caps out correctly around the `MaximumFeedLength` eviction boundary
      (does not exceed 10,000, does not show a stale pre-eviction value). Verified with an 11,000
      multi-subject burst (`nats bench pub`) landing exactly at `10000`, including at 5 digits
      where the label's dynamic width first meets the AnchorEnd gutter margin.

## 4. Spec sync

- [x] 4.1 Run `openspec validate --change "live-feed-message-count" --strict` and fix any
      reported issues.

## 5. Follow-up refinement: position, padding, following/sticky forms

User feedback after the above shipped: move the readout to the bottom-right corner, pad it, and
give it distinct following/sticky forms instead of a bare count. See design.md's revised
Decisions 1-3.

- [x] 5.1 Replace `LiveUpdatesView`'s `CountChanged` event with
      `public event Action<LiveFeedStatus>? StatusChanged;`, where
      `LiveFeedStatus(int Count, bool Following, int SelectedIndex)` is a new `internal readonly
      record struct` in `LiveFeed/LiveUpdatesView.cs`.
- [x] 5.2 Add a private `RaiseStatusChanged()` helper and call it from `OnEvent` (replacing the old
      `CountChanged?.Invoke(...)`), `Clear`, `ToggleFollow`, and the `_listView.ValueChanged`
      handler's not-suppressed branch (which also flips `_following = false` there).
- [x] 5.3 In `MainWindow.cs`, replace the count-only label wiring with a local static
      `FormatFeedStatus(LiveFeedStatus)` function producing ` {count} ↓ ` while following or
      ` {position}/{count} ● ` while sticky, used both for the label's initial `Text` and its
      `StatusChanged` handler.
- [x] 5.4 Move the label's `Y` from `Pos.Top(feedFrame)` to `Pos.Bottom(feedFrame) - 1` (bottom
      border row); keep `X = Pos.AnchorEnd() - 2` (already width-aware, so it holds for the wider
      sticky `n/n` form too).
- [x] 5.5 Rebuild and re-verify under `tmux`: following-mode text updates live on arrival; pressing
      Up while following switches to sticky (`position/total ●`) with the correct position;
      further arrivals while sticky update the total but preserve the position; `Space` resumes
      following (`total ↓`); the bottom-right placement and padding are visible in the captured
      pane.
- [x] 5.6 Update `proposal.md`, `design.md`, and `specs/live-feed/spec.md` to reflect the revised
      shape (this was an in-flight refinement, not a new change).
- [x] 5.7 Re-run `openspec validate --change "live-feed-message-count" --strict`.

## 6. Follow-up refinement: color the status text

User feedback: color the whole status text green while following, yellow while sticky (a red
"inactive" tier is anticipated but has no corresponding state yet). A first attempt colored only
the trailing glyph via a `DrawingContent` single-cell recolor; simplified to a whole-label
override per follow-up feedback that this was more complexity than wanted. See design.md's
Decision 6.

- [x] 6.1 Add `Theme.LiveFeedFollowingColor` (green) and `Theme.LiveFeedStickyColor` (yellow) to
      `Theme.cs`, alongside the existing `EditableBackground`.
- [x] 6.2 In `MainWindow.cs`, capture the label's ambient `VisualRole.Normal` background once
      (`feedStatusBackground`, read before the label's first `SetScheme` call) and call
      `feedStatusLabel.SetScheme(new Scheme(new Attribute(color, feedStatusBackground)))` at
      construction (following color) and in the `StatusChanged` handler (following/sticky color
      matching the new `Following` value), alongside the existing `Text` update.
- [x] 6.3 Rebuild and re-verify under `tmux` using `capture-pane -e` (ANSI-escape capture, since
      plain-text capture can't show color) to confirm the whole status text renders green while
      following and yellow while sticky.
- [x] 6.4 Update `proposal.md`, `design.md`, and `specs/live-feed/spec.md` to reflect whole-text
      coloring (this was an in-flight refinement, not a new change).
- [x] 6.5 Re-run `openspec validate --change "live-feed-message-count" --strict`.
