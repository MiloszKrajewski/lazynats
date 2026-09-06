## Context

`feedFrame` (a `FrameView` titled `" Live Feed "`, constructed in `MainWindow.cs`) hosts
`LiveUpdatesView`, which owns an `ObservableCollection<FeedEnvelope>` capped at
`MaximumFeedLength = 10_000` (evicting from the front once exceeded). Nothing today surfaces how
many messages are currently buffered. The `live-feed` spec's "No In-View Header" requirement
means `LiveUpdatesView` itself must not render this text — framing is the host container's job —
so the count has to be displayed on `feedFrame`'s own border, owned by `MainWindow`.

Exploration (referenced in `proposal.md`) already settled the placement mechanics: a `Label`
added as a **sibling** of `feedFrame` (not inside `feedFrame.Border`, not inside
`LiveUpdatesView`), positioned with `Pos`/`Dim` to visually overlay a border row. Because it draws
after `feedFrame`'s own draw pass (siblings draw in `Add` order), no `LineCanvas.Exclude`
bookkeeping is needed. This design fixes the remaining details: how status gets from
`LiveUpdatesView` to `MainWindow`, exactly how the label is positioned, and exactly which
`feedFrame` events force it to redraw.

A first iteration of this change shipped a bare count on the top border row. A follow-up round of
feedback (during the same change, before archiving) asked for four refinements: move the readout
to the bottom-right corner, pad it with surrounding spaces, and give it two distinct forms tied to
`LiveUpdatesView`'s existing follow/pause behavior (`_following`, toggled by `Space`/`Command
.Toggle`) — the total count with a down arrow while following, or the paused selection's position
out of the total with a filled circle while "sticky". This design reflects that revised shape
directly rather than layering a changelog on top of the original.

## Goals / Non-Goals

**Goals:**
- Show the live feed's current status — buffered count, and follow/sticky state — on `feedFrame`'s
  bottom-right border corner, updated on arrival, eviction, `Clear`, and follow/pause toggling
  (including the user moving the selection while paused).
- Keep `LiveUpdatesView` free of any rendering of this text (per "No In-View Header") — it only
  exposes the status as data.
- Keep the label correctly painted across `feedFrame` resize and focus changes, despite sibling
  views not cascading dirty-tracking to each other.

**Non-Goals:**
- Further visual polish beyond the two status forms (color, additional fields).
- Any change to eviction behavior, `MaximumFeedLength`, or dedup logic.
- Any change to the follow/pause behavior itself (`Space` toggling, selection-shifts-on-eviction) —
  only its visibility on the border.

## Decisions

### 1. `LiveUpdatesView` exposes status via a plain event carrying a small record struct
Add:
```csharp
internal readonly record struct LiveFeedStatus(int Count, bool Following, int SelectedIndex);
public event Action<LiveFeedStatus>? StatusChanged;
```
`SelectedIndex` (0-based) is only meaningful when `Following` is `false`; while following, the
feed is always scrolled to the newest message so there's no independent position to report.
`StatusChanged` is raised (via a private `RaiseStatusChanged()` helper reading `_events.Count`,
`_following`, and `_listView.SelectedItem ?? 0`) from four places: the end of `OnEvent` (after the
eviction loop, so it reports the post-eviction count and any eviction-shifted selection), the end
of `Clear` (reporting `0`), `ToggleFollow` (after flipping `_following` and, if now following,
snapping to the end), and the `_listView.ValueChanged` handler's not-suppressed branch (the user
moved the selection, which both flips `_following` to `false` and changes the position to
display). This mirrors the existing `ItemSelected` event on the same class and the `StatusChanged`
pattern already used by `StreamsTab`/`ValuesTab`/`ObjectsTab`, consumed directly in
`MainWindow.cs` — no new event-plumbing idiom is introduced. A single combined event (rather than
separate `CountChanged`/`FollowingChanged` events) keeps the three fields atomically consistent
for whatever `MainWindow` renders from them — a two-event split could observe a stale count against
a fresh follow flag (or vice versa) depending on subscriber ordering.

Alternative considered: exposing `IObservable<LiveFeedStatus>` (the class already uses Rx for its
own feed subscription). Rejected — the consumer (`MainWindow`) does no filtering/buffering/
composition on this stream, just a direct format-and-assign, so a plain event avoids pulling
`System.Reactive` into `MainWindow` for no benefit and stays consistent with the sibling
`StatusChanged` events it already wires the same way.

### 2. Label lives in `MainWindow`, formatted by a local `FormatFeedStatus` function
```csharp
static string FormatFeedStatus(LiveFeedStatus status) => status.Following
    ? $" {status.Count} ↓ "
    : $" {status.SelectedIndex + 1}/{status.Count} ● ";

var feedStatusLabel = new Label { X = ..., Y = ..., Width = Dim.Auto(), Height = 1, CanFocus = false,
    Text = FormatFeedStatus(new LiveFeedStatus(0, true, 0)) };
liveUpdates.StatusChanged += status => { feedStatusLabel.Text = FormatFeedStatus(status); feedStatusLabel.SetNeedsDraw(); };
```
`CanFocus = false` (Label's default) keeps it out of the Tab order — it's status text, not a
control. `FormatFeedStatus` is a local static function (used both for the label's initial text,
matching `LiveUpdatesView`'s actual initial state of empty-and-following, and for every
`StatusChanged` update) rather than a method on `LiveUpdatesView` or a separate class, since it's
purely a rendering concern local to this one call site. The down arrow (`↓`, U+2193) reads as
"following down" as new messages arrive; the filled circle (`●`, U+25CF) reads as
stopped/pinned — both single-column glyphs, consistent with the box-drawing/block glyphs already
used elsewhere in this codebase (`EditFrame`, `LiveLogDataSource`'s scrollbar). Each form is
wrapped in a leading and trailing space for breathing room against the border line, per explicit
request — plain digits/glyphs directly on a border row read as cramped.

### 3. Positioning: right-anchored on `feedFrame`'s bottom border row, inset from the corner
`feedFrame` is `X = 0, Width = Dim.Fill()`, so its right edge coincides with `MainWindow`'s right
edge, and `feedStatusLabel` (a `MainWindow`-relative sibling) can position directly against
`MainWindow`'s own bounds. `Width = Dim.Auto()` sizes the label to its text, and
`X = Pos.AnchorEnd() - 2` places it two columns short of `MainWindow`'s right edge — one to clear
the border's corner glyph's own column (`Pos.AnchorEnd()` alone is flush *with* that column), one
more for an actual blank gutter column before it (confirmed empirically: `- 1` still abuts the
corner with no visible gap). `Pos.AnchorEnd()` with no offset is the width-aware form (X tracks the
label's *current* resolved `Width`, recomputed on each layout pass, unlike `AnchorEnd(n)`'s fixed
offset), which matters here because the label's width changes both with the message count's digit
count (1 digit up to 5, at the 10,000-message cap) and, in the sticky form, the position digit
count too — a fixed offset sized for one width would be overrun by a wider one and start
overwriting the corner glyph. `Y = Pos.Bottom(feedFrame) - 1` places it on the bottom border row
(one row above `feedFrame`'s own bottom edge, in the same coordinate space as `MainWindow` since
`feedFrame.Y = Pos.Bottom(tabs)`), per the explicit request to move the readout from the top
border to the bottom-right corner.

Alternative considered: positioning the label as a child of `feedFrame` (offset to land on the
border row directly). Rejected in the exploration this design formalizes — a child of `feedFrame`
positioned outside `feedFrame`'s own content area interacts with `FrameView`'s own
clipping/`LineCanvas` painting order in ways that required extra bookkeeping
(`LineCanvas.Exclude`) to avoid being erased by the border's own paint pass; a sibling with a
later draw order sidesteps that entirely.

### 4. Redraw triggers: `feedFrame.FrameChanged` and `feedFrame.HasFocusChanged`
```csharp
feedFrame.FrameChanged += (_, _) => feedStatusLabel.SetNeedsDraw();
feedFrame.HasFocusChanged += (_, _) => feedStatusLabel.SetNeedsDraw();
```
`FrameChanged` fires whenever `feedFrame`'s own `Frame` (position/size) changes — covers terminal
resize, which repaints the whole border including the row the label sits on. `HasFocusChanged`
covers `feedFrame`'s border potentially repainting with a different scheme when focus moves
into/out of its subtree (e.g. into `liveUpdates` via Alt+M) — the same hook `EditFrame` already
uses (`child.HasFocusChanged += (_, _) => SetNeedsDraw();`) to know when to recolor. Terminal.Gui
dirty-tracking does not cascade sideways between siblings, so without these, the label could be
painted-over and not redrawn until something coincidentally invalidates it.

### 5. Where the wiring lives
All of the above (label construction, `StatusChanged` subscription, `FrameChanged`/
`HasFocusChanged` subscriptions) lives inline in `MainWindow`'s constructor next to where
`feedFrame`/`liveUpdates` are already constructed — consistent with how `streamsStatusShortcut`
etc. are wired a few lines later in the same file. No new type is introduced for a single label
with three subscriptions plus a local formatting function.

### 6. Indicator coloring: whole-label `Scheme` override, not per-run markup
A plain Terminal.Gui `Label` has no inline "markdown-like" run coloring — it always paints its
whole `Text` in one color per draw pass. Rather than singling out just the trailing glyph (tried
first, then dropped as unnecessary complexity for what it bought), the entire label is recolored
as one unit via the documented per-view override pattern (`doc/terminal-gui-howto.md`,
"Per-view color overrides"):
```csharp
var feedStatusBackground = feedStatusLabel.GetAttributeForRole(VisualRole.Normal).Background;
feedStatusLabel.SetScheme(new Scheme(new Attribute(Theme.LiveFeedFollowingColor, feedStatusBackground)));
liveUpdates.StatusChanged += status => {
    feedStatusLabel.Text = FormatFeedStatus(status);
    var color = status.Following ? Theme.LiveFeedFollowingColor : Theme.LiveFeedStickyColor;
    feedStatusLabel.SetScheme(new Scheme(new Attribute(color, feedStatusBackground)));
    feedStatusLabel.SetNeedsDraw();
};
```
`feedStatusBackground` is read once, before the label's first `SetScheme` call, so later
`StatusChanged` updates keep re-deriving from the *original* ambient background rather than
whatever the previous override left in place. `Theme.LiveFeedFollowingColor` (green) and
`Theme.LiveFeedStickyColor` (yellow) are new `Theme.cs` constants, per this project's "tunable
colors live in `Theme.cs`" convention.

Alternative considered: recoloring only the trailing glyph cell (via the `DrawingContent` event,
firing after the base `Text` draw, to repaint just the one cell at `Text.Length - 2`). This was
the first implementation and worked, but added a second code path (a `DrawingContent` handler
duplicating the glyph-position logic already implicit in `FormatFeedStatus`) for a distinction
between "digits" and "glyph" coloring that wasn't actually wanted — simplified away once the
request became "color the whole thing."

## Risks / Trade-offs

- **[Risk]** `Dim.Auto()` width recompilation on every `StatusChanged` could in principle shift
  the label's left edge as digit-count changes (e.g. 999 → 1000, or switching between the shorter
  following form and the longer `n/n` sticky form), which combined with right-anchoring is the
  intended behavior (right edge stays fixed, left edge grows/shrinks) but is worth confirming
  visually. → **Mitigation:** verified manually via `tmux capture-pane`, including a burst crossing
  1-, 2-, 3-, and 5-digit counts and a following/sticky toggle, confirming the gutter column
  survives every width.
- **[Risk]** High-frequency feeds already batch arrivals through `LiveUpdatesView`'s existing
  `Buffer(BufferWindow)` (25ms) before calling `OnEvent` once per envelope in the batch — so
  `StatusChanged` still fires once per envelope, not once per batch, which is a possible n-per-tick
  cost on the label under sustained load. → **Mitigation:** setting `Label.Text` and calling
  `SetNeedsDraw()` are cheap (no layout pass), and Terminal.Gui coalesces redraws to once per
  main-loop iteration regardless of how many times `SetNeedsDraw()` is called within it — no
  additional throttling needed.
- **[Trade-off]** A plain `event Action<LiveFeedStatus>?` (vs. `IObservable<LiveFeedStatus>`)
  means `MainWindow` can't compose/throttle this stream later without `LiveUpdatesView` changing
  its public surface. → Accepted: matches the existing `ItemSelected`/`StatusChanged` precedent in
  this codebase; can be revisited if a future change needs Rx composition here.
