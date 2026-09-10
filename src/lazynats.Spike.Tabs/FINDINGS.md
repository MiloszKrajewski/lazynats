# Findings

Notes recorded as the spike progresses (task 2.4 onward), for a later `lazynats` adoption change
to build on without re-deriving. All confirmed live, against the actually-installed Terminal.Gui
`2.4.17`, via `tmux capture-pane -p -e` and/or direct interactive runs.

## `LineCanvas.Exclude` (task 2)

- **Which object's `LineCanvas` to call `Exclude` on**: the *content* View's own `LineCanvas`
  property (the View whose `BorderStyle`/`Border` you set) - not
  `Border.GetOrCreateView().LineCanvas`. Matches the XML doc on `View.LineCanvas`: "Border adds
  lines to this LineCanvas." `Border.GetOrCreateView()` (a `BorderView`) is a real `View` and does
  have its own `LineCanvas` property (every `View` does), but it's a distinct, effectively-unused
  object for this purpose - the actual border line segments live on the *owning content view's*
  canvas.
- **Coordinates**: `Exclude(Region)` expects **screen coordinates**, not viewport- or
  frame-relative. Convert via `borderView.ViewportToScreen(rect)` (or equivalent) before calling.
- **Confirmed live**: excluding a caption-sized rectangle from the top border line suppresses only
  those cells; the border still closes correctly on both sides with intact corners, even with two
  captions placed immediately adjacent to the left and right corners simultaneously (no stray or
  mismatched glyph at either end).

## Painting a caption into the excluded rectangle (task 2.3)

- **Does NOT work**: overriding the content View's own `OnDrawingAdornments`/`OnDrawingContent`
  and calling `Move(col, row)` / `AddStr(...)` with a row outside the View's `Viewport` (e.g. `-1`
  to reach the top border row when `Thickness.Top == 1`). Confirmed live: the `Exclude` call still
  visibly punches the gap in the border, but nothing gets painted into it - `AddRune`/`AddStr`
  silently no-op outside the "visible content area" exactly as their XML docs say, and this bound
  check is **not** relative to the current clip region.
- **Dead-end explored and abandoned**: temporarily widening the clip via `SetClipToScreen()` before
  painting, then attempting to restore it. This does not unblock the row-bound rejection above
  *and* is actively dangerous: `View.GetClip()` returns the live, single, applicationwide `Region`
  object, not a clone ("This method returns the current clip region, not a clone" - confirmed by
  its own XML doc, and confirmed live: a save/expand/restore sequence corrupted an unrelated
  sibling `Label`'s rendering for the rest of the run, because "restoring" the saved reference was
  a no-op - it was the same mutated object). Anyone reaching for this pattern needs to `Clone()`
  the region first, and even then it doesn't solve the actual problem.
- **What works**: add the caption as an actual SubView (a `Label` is enough) of
  `Border.GetOrCreateView()` itself, positioned in that view's own local coordinate space. A
  `BorderView` has no border/padding of its own, so its `Viewport` spans the entire border-
  thickness ring 1:1 with its `Frame`, and row `0` addresses the top border line directly with no
  negative-coordinate trick needed. `BorderView` then draws that SubView through the ordinary
  SubView-draw pipeline, so there's no row-bound rejection.
- `BorderView`'s own `DrawingContent` event never fires (confirmed live, counter stayed `0`) - it
  draws no "content" of its own in legacy (non-`Tab`) border mode, only the line-canvas geometry.
  Don't hook that event expecting to intercept anything.
- **Confirmed live** (via `tmux capture-pane -p -e`, reading actual ANSI attributes): the caption's
  attribute (`black` fg / `BrightYellow` bg) is distinct from the surrounding border cells and
  survives the frame's line-canvas render pass unmodified - painted first (as a SubView, drawn in
  the normal SubView pass), excluded second (`Exclude` just filters what the *later* line-canvas
  flush is allowed to overwrite), so draw order works out even though the code calls `Exclude` from
  a separate hook (`OnDrawingAdornments`) than where the SubView itself draws.
- Did not separately re-check this outside tmux: `doc/focus-accent-color.md`'s tmux/psmux gotcha is
  specifically about a **color-only** change (same rune, different attribute) failing to reach the
  live display even though `capture-pane -p -e` reports it correctly. Here the caption's *rune*
  (letters) and its attribute both differ from the border's (`─`/`┌`/`┐`, default scheme), which is
  exactly the "pair a color check with a rune change" mitigation that document recommends - so the
  known gotcha doesn't apply and the tmux-only confirmation should be reliable. Worth an actual
  outside-tmux glance before adoption if anyone wants extra certainty, but not treated as a gap
  here.

## Keyboard navigation (task 4)

A container View (here, `TabbedView` itself, the SuperView of every tab's content) **cannot reliably
reclaim focus for itself** via its own `SetFocus()`. Confirmed live, repeatedly, across several
variations: it returns `false` and leaves focus exactly where it was. Root cause (consistent with
the `PreviouslyFocused` XML doc: "allows focus to be restored to the same subview when focus
returns to this view"): a container's own `SetFocus()` delegates back down into whatever was
`PreviouslyFocused` rather than becoming the leaf focus target itself, so a "focus the header"
operation attempted as `this.SetFocus()` just re-focuses the same content it was trying to leave.

This directly explains why `ManagementTabs.cs` in the main app never calls `Value.SetFocus()` to
focus a header - it calls `SetFocus()` on `Value.Border.View`'s separate `TitleView` SubView
instead, a genuinely different node in the tree, not an ancestor of the content. **`TabbedView`
mirrors that shape**: a dedicated 1×1 `_headerProxy` (`CanFocus = true`) added as a real SubView of
`Border.GetOrCreateView()`, representing "the header has focus." `HeaderHasFocus` checks
`App.Navigation.GetFocused() == _headerProxy`, never `== this`.

Three more focus-system specifics, each cost real time and are worth recording precisely:

- **`CanFocus` must be `true` on *every* ancestor for a descendant to receive focus at all**, not
  just the descendant itself. Confirmed live twice: (1) a perfectly focusable, visible Button
  inside `content` couldn't take focus via `SetFocus()` (silently returned `false`) until the
  `content` View itself was given `CanFocus = true`; (2) `_headerProxy` couldn't take focus until
  `_borderView` (`Border.GetOrCreateView()`, `_headerProxy`'s direct SuperView) was *also* given
  `CanFocus = true` - `TabbedView.CanFocus = true` alone was not enough, because `_borderView` sits
  between them in the ancestor chain.
- **Blurring a focused leaf only cascades focus up to its nearest focusable ancestor, not all the
  way out.** Confirmed live: blurring a focused `Button` (`button.HasFocus = false`) left its
  container `content` View itself focused, not nothing - and a subsequent `SetFocus()` on an
  unrelated branch (e.g. `_headerProxy`) still failed against that new holder. The general-purpose
  `BlurCurrentFocus()` helper this control uses loops `GetFocused()?.HasFocus = false` (bounded, a
  handful of iterations) until nothing is focused, before any cross-branch `SetFocus()` call -
  mirrors `ManagementTabs.FocusOwnContent`'s single `headerView.HasFocus = false` line, generalized
  because our tree has more than one such transition point (header proxy, and every prior tab's
  content).
- **Terminal.Gui's default startup focus assignment picks the first `CanFocus` View it finds,
  depth-first - which can be a *container*, not a leaf.** Confirmed live: with only `TabbedView`
  itself `CanFocus = true` (before `_headerProxy` existed), startup focus landed on `TabbedView`
  itself, not any tab's content. Redirecting that to the initially-selected tab's content (matching
  `ManagementTabs`' own startup behavior) has to happen **after** that first draw pass, not during
  it - calling `SetFocus()` synchronously from inside the View's own first `OnDrawingAdornments`
  silently returned `false`. Deferred via `App.AddTimeout(TimeSpan.Zero, ...)`, the same pattern
  `MainWindow.cs` already uses for exactly this class of re-entrancy problem (see its own Alt+P
  comment).

Once those were in place, `Up`/`Down`/`Left`/`Right`/`Alt+1..N` all worked exactly per spec,
confirmed live via `tmux capture-pane -p -e` (reading the header-focused vs. selected-but-
content-focused caption attribute, which are deliberately different colors specifically so this
distinction is independently verifiable, not just inferred from which widget the diagnostic label
says holds focus) - see `openspec/changes/spike-tab-strip-control/specs/tab-strip-spike/spec.md`'s
"Native Keyboard Navigation" scenarios.

**Tab/Shift+Tab (task 4.4)**: needed no special-casing at all, unlike `ManagementTabs.cs`'s
`TabGroup`-routing workaround for `Tabs`. Confirmed live: with no `TabBehavior` set explicitly,
default Tab/Shift+Tab cycles cleanly between a tab's focusable content and `_headerProxy` and
does not leak focus to anything else in the app (`MainWindow`'s diagnostic label, unrelated
SubViews, etc.). The workaround `ManagementTabs.cs` needs exists specifically because `Tabs` sets
`TabStop = TabBehavior.TabGroup` on itself; a from-scratch control that never sets that doesn't
inherit the problem. Worth re-verifying once real (multi-focusable-descendant) tab content is
substituted for the single-`Button` placeholders used here, but the base mechanism needs no
extra code.

## Mouse selection (task 5)

**Confirmed live** (interactive run, not just tmux): `_borderView`'s `MouseEvent` handler
(`OnBorderMouseEvent`) fires correctly for clicks on caption text, despite `_headerProxy` and each
caption's own `Label` being real SubViews of `_borderView` sitting in front of it in the tree -
they don't intercept/consume the click first, so no extra `Handled`-suppression or SubView-level
click routing was needed beyond the single handler on `_borderView` itself. Clicking an
unselected tab's caption selects it and focuses its content, same as the keyboard path
(`SelectAndFocusContent`). Also confirmed working when clicking inside the tab's own content
area (not just the caption) - ordinary Terminal.Gui click-to-focus on the content SubView, no
extra code needed for that part.

## Horizontal overflow (task 6) - not yet implemented, deferred

**Confirmed live, and left unaddressed for now**: with no scroll-offset logic yet (`RecomputeLayout`
lays out every tab unconditionally, per its current implementation), a caption run wider than the
strip simply truncates - the last caption(s) run off the right edge instead of being hidden behind
a scroll window, and there is no `<`/`>` off-screen cue. This is the "Horizontal Overflow
Scrolling" requirement's scenarios failing outright, not a partial/edge-case gap.

Deferred deliberately: the real app's first adoption usage (`Subscribe`/`Streams`/`Values`/`Objects`/
`Publish`, per `Program.cs`'s placeholder captions) comfortably fits under 80 columns, so this
doesn't block trying the control out. Left as explicit follow-up work (tasks 6.1-6.4 remain
unchecked) rather than solved speculatively - the scroll-offset design is already sketched in
`design.md` ("Overflow tracked in tab units, not columns") for whoever picks this back up.
