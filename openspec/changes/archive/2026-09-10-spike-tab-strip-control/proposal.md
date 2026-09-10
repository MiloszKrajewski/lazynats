## Why

`doc/focus-accent-color.md` documents an abandoned attempt to give the focused tab/frame a color
accent, so Alt+0..5 focus jumps have a visible clue. It hit two dead ends in Terminal.Gui's
built-in `Tabs`: whole-border recoloring breaks at tab-frame seams (`LineCanvas`'s intersection
resolution has no focus-aware tie-break), and title-text-only recoloring never found where a
`Tabs` header's own attribute actually comes from. A from-scratch, single-line tab strip that
paints its own captions — carving them out of the container's border via `LineCanvas.Exclude`
rather than compositing separate bordered per-tab frames — sidesteps both dead ends structurally,
but rests on Terminal.Gui APIs and draw-order behavior that need confirming live against the
actually-installed version before any of it is trusted (this doc's own history is one prior
example of docs/memory not matching the installed version). A standalone spike, mirroring
`src/lazynats.AotProbe`'s role as a testing ground independent of the main app, is the way to
de-risk that before writing a real design for `lazynats` itself.

## What Changes

- Add `src/lazynats.Spike.Tabs`, a standalone Terminal.Gui console app (not part of
  `lazynats.sln`, no NATS dependency), to prototype and validate a custom-drawn tab strip control
  in isolation.
- The spike validates, against the actually-installed Terminal.Gui version:
  - `LineCanvas.Exclude(Region)` suppresses a border's output in a carved-out rectangle while the
    border still auto-joins correctly around it.
  - Draw order lets custom-painted caption text (per-tab focus/selection color) survive
    un-overwritten by the border's own line-canvas render pass.
  - A fully custom control (no `Tabs`/`ManagementTabs` base class) can natively reproduce
    `ManagementTabs`' keyboard behavior: Up/Down header↔content focus toggle, Left/Right tab
    switch, Tab/Shift+Tab, Alt+N direct select — using this codebase's existing `Visible`-toggle
    pattern (`DrillableListView`/`PollingDetailsView`) for content switching instead of `Tabs`'
    `Value`-based reparenting.
  - Mouse click selects a tab, hit-tested against the same caption-rectangle table used for
    drawing.
  - Horizontal overflow scrolls by whole tabs (never cutting a caption mid-word), auto-scrolls to
    keep the selected tab fully visible, and shows a cue when tabs exist off-screen in a
    direction.
- Explicitly out of scope for the spike: NATS/JetStream integration, the real app's tab content
  (Subscribe/Streams/Values/etc.), and AOT/trimming validation (that remains `AotProbe`'s job) —
  the spike exercises the control in isolation with placeholder pages, the same way `AotProbe`
  validates one pattern rather than the whole app.
- Nothing in `src/lazynats` (the main app) changes as part of this proposal. `ManagementTabs` and
  `Tabs` stay exactly as they are; adopting the spiked control into the main app is a separate,
  later change, contingent on the spike succeeding.

## Capabilities

### New Capabilities
- `tab-strip-spike`: A standalone spike project validating a custom-drawn, `LineCanvas.Exclude`-based
  tab strip control (rendering, keyboard navigation, mouse selection, horizontal overflow) ahead of
  any adoption into the main `lazynats` app.

### Modified Capabilities
(none — this change only adds a new standalone project; no existing spec's requirements change)

## Impact

- New project: `src/lazynats.Spike.Tabs` (own `.csproj`, not referenced by and not referencing
  `src/lazynats`, not added to `lazynats.sln`, mirroring how `src/lazynats.AotProbe` is excluded
  from the solution).
- No changes to `src/lazynats` (`MainWindow.cs`, `Components/ManagementTabs.cs`, `Tabs` usage) —
  those are only touched by a future, separate change if/when this spike's control is adopted.
- No changes to `.nuke/build/Program.cs` or CI — the spike is a manually-run testing ground, same
  as `AotProbe`'s `test-aot.ps1` is manually run rather than wired into CI.
