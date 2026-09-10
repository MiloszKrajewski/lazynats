## Context

`doc/focus-accent-color.md` records two dead ends hit while trying to accent-color the focused
tab/frame using Terminal.Gui's built-in `Tabs`: whole-border recoloring breaks at tab-frame seams
because `LineCanvas`'s intersection resolution (`GetAttributeForIntersects`) has no focus-aware
tie-break, and title-text-only recoloring never found where a `Tabs` header's own attribute
actually comes from (tried the tab's `Border`, `TitleView`, `TitleView.Border`, and the content
view itself — none of them worked). That document also records a hard-won lesson: everything in
it was confirmed against Terminal.Gui 2.4.17 specifically, and the `Border`/`BorderView` split
already invalidated a design's assumptions once when the installed version moved.

A different approach, worked out in conversation rather than tried yet: keep a normal, full
`Border` on the tab-strip container (real corners, real left/right/bottom lines) but call
`LineCanvas.Exclude(Region)` — documented for exactly this ("overlaying elements like title labels
on borders, ensuring the border auto-joins correctly") — to carve each caption's rectangle out of
the border's own cell-map output, then paint captions ourselves with per-tab focus color. The View
draw pipeline (`DoDrawAdornments → … → DoDrawContent → … → DoRenderLineCanvas → DoDrawComplete`)
runs the actual line-canvas flush *after* content drawing and skips excluded cells, so nothing
should overwrite what we paint. This sidesteps both prior dead ends structurally — no adjacent
bordered subviews merging at a seam, no `TitleView` attribute resolution involved at all — but it
is exactly the kind of claim `focus-accent-color.md` warns not to trust from docs alone.

`src/lazynats.AotProbe` is this repo's existing precedent for a standalone project used to
validate a pattern in isolation before it's relied on in the main app — this change follows that
shape for structure, but deliberately *not* for dependency choice (see Decisions).

## Goals / Non-Goals

**Goals:**
- Confirm, live, against the actually-installed Terminal.Gui version: `LineCanvas.Exclude(Region)`
  suppresses a border's output in a carved-out rectangle while the border still auto-joins
  correctly around it, and identify exactly which object (`View.Border`,
  `Border.GetOrCreateView()`, or something else) exposes the `LineCanvas` to call it on.
- Confirm custom-painted caption text survives un-overwritten by the border's own
  `DoRenderLineCanvas` pass.
- Build one new `View` subclass that natively reproduces `ManagementTabs`' keyboard behavior
  (header↔content focus toggle, tab switch, Tab/Shift+Tab, direct select) without inheriting from
  `Tabs`/`ManagementTabs`.
- Support mouse click to select a tab, hit-tested against the same layout data used for drawing.
- Support horizontal overflow as a whole-tab scroll window that keeps the selected tab fully
  visible and cues when tabs exist off-screen.

**Non-Goals:**
- Adopting the resulting control into `src/lazynats` — that is a separate, later change,
  contingent on this spike succeeding.
- NATS/JetStream integration, or any of the real app's tab content.
- AOT/trimming validation — that remains `src/lazynats.AotProbe`'s job specifically.
- CI wiring — this is a manually-run testing ground, same as `AotProbe`'s `test-aot.ps1`.
- Performance work — five-or-so placeholder tabs, no large-N concerns.

## Decisions

- **Project shape**: standalone SDK-style console app, `net10.0`, `OutputType Exe`, not added to
  `lazynats.sln`, not referencing and not referenced by `src/lazynats` — mirrors `AotProbe`'s
  isolation. Unlike `AotProbe`, no `PublishAot` (AOT validation is explicitly out of scope here).
- **Terminal.Gui version: pin exactly `Terminal.Gui` `2.4.17`, matching `src/lazynats.csproj` —
  not `AotProbe`'s `Terminal.Gui.Editor` `2.5.7`.** `AotProbe` happens to reference a newer,
  different package; copying that reference would make the spike validate a version the main app
  doesn't actually use, defeating the point given how version-sensitive this exact API surface
  already proved to be in `focus-accent-color.md`.
- **Control owns captions explicitly**: an `AddTab(string caption, View content)`-style API,
  rather than reading `Title` off each added content view the way `Tabs` does today. Matches the
  marginal lean from the design discussion (caption layout/overflow math wants an explicit list
  up front) and is the simpler thing to build first. Whether the *eventual* real control (if
  adopted) should instead mirror `Tabs`' per-view-`Title` convention is explicitly deferred, not
  decided here — see Open Questions.
- **Content switching via `Visible`, not reparenting**: all added content views stay as permanent
  SubViews; selecting a tab toggles `Visible`/`CanFocus`. Mirrors this codebase's existing
  `DrillableListView`/`PollingDetailsView` pattern instead of reimplementing `Tabs`' own
  `Value`-based child swapping.
- **Keyboard nav is native to the new control**: `AddCommand`-registered handlers directly on the
  new `View` subclass, not layered on `Tabs`. The Tab/Shift+Tab routing quirk `ManagementTabs.cs`
  works around (`Tabs` sets `TabStop = TabBehavior.TabGroup`, routing Tab/Shift+Tab straight to
  the nearest `TabGroup` rather than bubbling) does not automatically apply to a from-scratch
  control — whatever `TabBehavior` this control picks needs its own live verification, not an
  assumption that the old workaround is still needed or still sufficient.
- **Mouse hit-testing reuses the drawing layout**: `MouseEvent`/`OnMouseEvent` maps
  viewport-relative `Position.X` against the same caption-rectangle table used for drawing and for
  the `LineCanvas.Exclude` calls — one layout computation, three consumers (draw, exclude,
  hit-test).
- **Overflow tracked in tab units, not columns**: a `scrollOffset` in whole tabs, recomputed on
  layout/resize and on selection change, so a caption is never shown partially cut. Selecting a
  tab that's scrolled out of view (via Left/Right, Alt+N, or a click near an edge) auto-scrolls it
  fully into view. Filler dashes at either end of the strip become `<`/`>` cues when tabs exist
  off-screen in that direction.
- **Verification method**: run interactively (`dotnet run --project src/lazynats.Spike.Tabs`) and
  drive it non-interactively via `tmux send-keys`/`capture-pane -p -e`, per this repo's own
  established technique — reusing two lessons `focus-accent-color.md` already paid for: spot-check
  color-only changes outside tmux too (psmux under-reports color-only cell updates), and pair a
  color check with a rune/`LineStyle` change when a tmux-only check specifically needs to observe
  a color transition.

## Risks / Trade-offs

- [Risk] `LineCanvas.Exclude` may live on a different object than expected in 2.4.17 (the
  `Border`/`BorderView`/`AdornmentView` split already surprised the prior attempt once) →
  [Mitigation] First implementation task is a minimal, isolated repro of `Exclude` alone, before
  building the rest of the control on top of an unverified assumption.
- [Risk] A from-scratch control reproducing `Tabs`' keyboard/focus behavior could rediscover the
  same fiddly edge cases `ManagementTabs.cs`'s comments describe, or hit new ones → [Mitigation]
  Budgeted as its own explicit validation goal, not an afterthought; reuse
  `App.Navigation.FocusedChanged` (not per-view `HasFocusChanged`) for any focus-driven visual
  state, per the already-documented modal-`Dialog` lesson.
- [Risk] The spike quietly becomes a second real implementation that never gets adopted →
  [Mitigation] `proposal.md` keeps adoption as a separate, later change; this design's Non-Goals
  exclude wiring it into the main app.

## Open Questions

- Should the eventually-adopted control (if this spike succeeds) keep `Tabs`' per-view-`Title`
  convention, or the spike's explicit `AddTab(caption, view)` shape? Deferred to whichever change
  proposes adoption into `src/lazynats` — not resolved here.
- Exact `TabBehavior`/Tab-key routing choice for the new control — left to empirical verification
  during the spike itself, not decided up front.
- Exact overflow-cue glyphs/placement (`<`/`>` vs alternatives) — left to implementation feel
  during the spike; captured as a firm decision only once tried live.
