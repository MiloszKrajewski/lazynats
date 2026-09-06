## Context

`LiveUpdatesView` (`src/lazynats/LiveFeed/LiveUpdatesView.cs`) backs the live feed pane. Today,
"following" is entirely derived: `wasFollowing = selectedItem is null || selectedItem ==
_events.Count - 1`, checked fresh on every message arrival. When the ring buffer evicts messages
from the front to stay within its `MaximumFeedLength` cap (10,000), nothing adjusts a not-following
selection's index, so a stationary cursor silently ends up pointing at whatever new message slides
into that row - confirmed empirically against the running app (select a message, flood past the
cap without touching any key, re-check via the detail dialog: a different message is now
selected). `Clear` has the same class of bug: it empties the message list but never resets
`SelectedItem`, leaving a stale highlight. There's also no way today to deliberately stay parked on
"the newest message right now" without later arrivals dragging the selection forward, because
being on the last row *is* the following signal.

This design covers the mechanism for all four pieces from the proposal: identity-preserving
rollover selection, `Clear` resetting selection, an explicit follow/pause state with `Space` as
the sole way to resume, and a vertical scrollbar.

## Goals / Non-Goals

**Goals:**
- Selection survives ring-buffer rollover by tracking the same message, not a fixed row index,
  falling back only once the selected message itself is evicted.
- Replace the derived `wasFollowing` check with an explicit, persisted follow/pause state that
  can be deliberately paused even while sitting on the newest message.
- Make `Space` the sole way to resume following, so "select the last message and stay there" is
  possible without a timing-dependent race.
- Fix `Clear` leaving a stale highlight.
- Add a vertical scrollbar so position within the buffer is visible while paused and browsing.

**Non-Goals:**
- The position/follow-state UI indicator (a border caption or similar showing e.g. "54/2435,
  following") - explicitly deferred to a follow-up ticket per the proposal's Impact section.
- Any change to the feed pipeline itself (envelope wrapping, channel merge, dedup, batching) -
  this is purely `LiveUpdatesView`'s own selection/navigation behavior.
- A horizontal scrollbar - `LiveLogDataSource.MaxItemLength` is deliberately pinned to `0` to
  avoid an O(n²) rescan-on-append; there is no meaningful horizontal extent to show a bar for.

## Decisions

### 1. O(1) decrement-on-evict, not an identity search
Eviction is always `_events.RemoveAt(0)` - never an arbitrary removal - so a not-yet-evicted
selection's new index is always exactly one less per eviction. This is a plain integer
decrement in the same loop that already does the eviction, no search required.

**Alternative considered:** an identity-based lookup after mutation, mirroring
`DrillableListView.ReplaceItems`'s `NearestIdentityCore` fallback search. Rejected: that
machinery exists because `ReplaceItems` can reorder or wholesale-replace its backing set; this
view's mutation shape is always "append one, optionally remove exactly one from the front," a
much narrower case that a search would only add O(n) cost to for no benefit.

### 2. Explicit persisted `_following: bool`, replacing the derived check
Following becomes a field, not a per-arrival computation from position. Manual navigation
(any key that actually moves the selection) always sets it `false`, unconditionally - including
when it lands on the last row. `Space` is the only thing that can set it `true`, and doing so also
jumps selection to the current newest message.

**Alternative considered:** keep deriving from position, only changing what counts as "the last
row" event. Rejected per direct discussion: if navigating to the last row ever resumes following on
its own, then pinning on the newest message without following requires `End` immediately followed
by `Space` before another message arrives - a timing-dependent race, not a real guarantee. Making
navigation-always-pauses unconditional removes the race entirely.

### 3. Reentrancy guard around this view's own `SelectedItem` writes
This view makes its own programmatic writes to `_listView.SelectedItem` (the rollover-decrement
while paused) and calls `_listView.MoveEnd()` (while following, and when `Space` resumes).
Terminal.Gui's `ValueChanged` event - already used elsewhere in this codebase (e.g.
`DrillableListView`) - appears from the framework's docs to fire generically whenever `Value` is
set, per its `IValue<T>`/Cancellable Work Pattern description, not only for user-interactive
changes. Left unguarded, this view's own writes would be indistinguishable from manual navigation
and would immediately re-pause `_following` on the very next line after we just set it - breaking
follow mode after exactly one message, every time.

Mitigation: a boolean field set immediately before, and cleared immediately after, each of this
view's own writes to `SelectedItem`/`MoveEnd()`; the `ValueChanged` handler that drives "manual
navigation pauses following" ignores changes observed while that field is set.

**This needs empirical verification during implementation**, not just this reasoning: does
`ValueChanged` fire for programmatic writes at all, and does a genuine no-op (e.g. pressing Down
while already on the last row) fire it too? Verify against the running app the same way the
rollover bug itself was confirmed (a load-gen-driven, tmux-observed test), rather than trusting
the framework docs alone - the docs were not conclusive.

### 4. Vertical-only scrollbar
`_listView.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;` - deliberately not
`HasScrollBars` (both axes). See Non-Goals: there's no real horizontal extent to report while
`MaxItemLength` stays pinned to `0`.

### 5. `Clear()` also resets selection
One-line addition: after `_events.Clear()`, explicitly clear `_listView.SelectedItem` too (the
same "no selection" value already used elsewhere in this file's `is null` checks). Same root
cause as the rollover bug - Terminal.Gui doesn't proactively reset a bare index in response to a
collection mutation - and the same fix shape: make it explicit rather than assumed.

## Risks / Trade-offs

- **[Risk]** `ValueChanged` may not cleanly distinguish user-driven vs. programmatic changes, or
  may not fire at all for some code paths (e.g. a true no-op keypress) → **Mitigation:** verify
  empirically before finalizing the guard's shape; if `ValueChanged` proves unreliable, fall back
  to setting the guard around the specific `Command` handlers (Up/Down/Home/End/PageUp/PageDown)
  instead of the value-change event.
- **[Risk]** The reentrancy guard adds statefulness to a view that was previously fully derived
  from position → **Mitigation:** keep the guard scoped tightly - set immediately before and
  cleared immediately after each of this view's own `SelectedItem`/`MoveEnd()` writes, nothing
  broader.
- **[Trade-off]** No user-visible signal yet distinguishes "paused because you navigated away"
  from "paused because you explicitly hit Space while on the newest message" from "following" -
  deferred to the follow-up status-indicator ticket. Until then, the only way to check current
  state is behavioral (press Space, see whether new messages start arriving at the bottom).

## Migration Plan

N/A - in-process UI behavior change with no data migration or external API surface.

## Open Questions

- Whether `Space` needs to be bound on `LiveUpdatesView.KeyBindings` (like `Key.C` today) or
  whether the inner `_listView` already claims `Space` for a built-in toggle/mark command that
  would shadow it first - genuinely unresolved from the framework's docs, needs to be checked
  against the running app during implementation (same class of check as `KeystrokeNavigator =
  null`, which already exists in this file to defuse a different built-in `ListView` behavior).
- Exact wording/placement of the future status indicator - out of scope here, tracked as a
  follow-up ticket per the proposal.
