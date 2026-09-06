## 1. Rollover Selection Fix

- [x] 1.1 In `LiveUpdatesView.OnEvent`, while a front-eviction is happening, decrement a
      not-on-the-evicted-row selection by one per eviction instead of leaving it at a stale index
      (already prototyped once this session - re-verify it still holds once integrated with the
      follow/pause rework in section 3, since that rework restructures `OnEvent`).
- [x] 1.2 Verify empirically (load-gen + tmux, per design.md Decision 3's "verify against the
      running app" approach): select a message, flood past the buffer cap without touching any
      key, confirm the same message stays selected until it is itself evicted, then confirm
      selection falls back to row 0.

## 2. Clear Resets Selection

- [x] 2.1 In `Clear()`, reset `_listView.SelectedItem` to its "no selection" value immediately
      after `_events.Clear()`.
- [x] 2.2 Verify empirically: select a message, press `C`, confirm no highlight remains rendered
      on the now-empty list.

## 3. Explicit Follow/Pause State

- [x] 3.1 Add a persisted `_following` bool field (default `true`), replacing the derived
      `wasFollowing` check currently computed at the top of `OnEvent`.
- [x] 3.2 Add a reentrancy-guard field around this view's own writes to
      `_listView.SelectedItem`/`MoveEnd()`, so they aren't misread as manual navigation.
- [x] 3.3 Wire `_listView.ValueChanged`: when not suppressed by the guard, unconditionally set
      `_following = false` (any manual navigation pauses, regardless of the row it lands on).
      Verify empirically whether `ValueChanged` fires for programmatic writes and for a true
      no-op keypress (e.g. Down while already on the last row) before finalizing this - fall back
      to guarding the specific navigation `Command` handlers instead if `ValueChanged` proves
      unreliable (design.md Risk 1).
- [x] 3.4 Bind `Space` (checking first whether it needs to go on `LiveUpdatesView.KeyBindings` or
      whether the inner `_listView` already claims it for a built-in command - design.md Open
      Questions): while following, pause without moving the selection; while paused, resume and
      jump to the newest message (`MoveEnd()`), guarded so this doesn't self-trigger 3.3's pause
      logic.
- [x] 3.5 Update `OnEvent`: while following, append + `MoveEnd()` (guarded); while paused, append
      + run the rollover-decrement logic from section 1 (guarded), selection untouched otherwise.
- [x] 3.6 Add a `Follow/Pause` (`Space`) entry to `Shortcuts` (`IShortcutSource`) alongside the
      existing `Clear` entry, so it's discoverable via the shortcut picker (`?`).
- [x] 3.7 Verify empirically: following persists across multiple consecutive arrivals (not just
      one - this is the failure mode an unguarded `ValueChanged` would produce); manual navigation
      landing on the last row does not resume following; `Space` resumes and jumps to the newest
      message from anywhere while paused; `Space` pauses in place (no selection movement) while
      following.

## 4. Vertical Scrollbar

- [x] 4.1 Set `_listView.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar` (vertical
      only - not `HasScrollBars` - per design.md's Non-Goals, given `LiveLogDataSource
      .MaxItemLength` stays pinned to `0`).
- [x] 4.2 Verify empirically via tmux capture that the scrollbar renders while paused and browsing
      a large buffered feed.

## 5. Spec Sync

- [ ] 5.1 Once implementation matches `specs/live-feed/spec.md`'s ADDED requirements, sync them
      into `openspec/specs/live-feed/spec.md` as part of archiving this change.
