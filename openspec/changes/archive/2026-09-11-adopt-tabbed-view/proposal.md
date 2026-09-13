## Why

`ManagementTabs` (`src/lazynats/Components/ManagementTabs.cs`) is built on Terminal.Gui's stock
`Tabs`, which renders each tab as its own separate bordered frame and needs a documented
`TabGroup`-routing workaround just to make Tab/Shift+Tab behave. `doc/focus-accent-color.md`
records an abandoned attempt to give the focused tab a color accent on top of that — it hit two
dead ends inside `Tabs` (whole-border recoloring breaks at tab-frame seams; title-text-only
recoloring never found where `Tabs`' header attribute actually comes from). The
`2026-09-10-spike-tab-strip-control` spike (archived under `openspec/changes/archive/`) built and
validated a from-scratch `TabbedView` control (`src/lazynats.Spike.Tabs/TabbedView.cs`) that
sidesteps both dead ends: a single continuous bordered box whose top border carries every
caption, carved out via `LineCanvas.Exclude`, with a genuine focus-accent color and no `Tabs`
workarounds needed. Rendering, keyboard nav (Up/Down/Left/Right/Alt+N/Tab/Shift+Tab), and mouse
selection are all confirmed live; only horizontal overflow scrolling was left unfinished. This
change adopts that control into the real app, replacing `ManagementTabs`/`Tabs` and closing the
"different frame color for active window" TODO item.

## What Changes

- Copy `TabbedView` (and its `TabbedViewSelectedIndexChangedEventArgs`) from
  `src/lazynats.Spike.Tabs` into `src/lazynats/Components/`, adjusting it from spike code to this
  codebase's conventions. `TabbedView` itself stays generic — it depends only on Terminal.Gui, not
  on any other `lazynats` component (`Theme`, `Services`, etc.); all `lazynats`-specific
  customization (theme colors) is applied by the derived `ManagementTabs`, not baked into
  `TabbedView`.
- Reimplement `ManagementTabs` on top of `TabbedView` instead of Terminal.Gui's `Tabs`, preserving
  its existing public surface used by `MainWindow` (`Add`/`SelectTab`) and the keyboard contract
  already specified in `tab-navigation` (Up/Down header↔content toggle, Left/Right tab switching,
  Alt+N direct select, Tab/Shift+Tab). **BREAKING** (internal only): `ManagementTabs`' tab
  registration moves from `Tabs`' per-view `Title`-property convention to `TabbedView`'s explicit
  `AddTab(caption, content)` call — `MainWindow` sets each tab's `N:Title` caption at
  registration time instead of via the content view's own `Title`.
- Remove the `Tabs`-specific `TabGroup` Tab/Shift+Tab workaround and its accompanying class-level
  comment in `ManagementTabs.cs` — confirmed in the spike (FINDINGS.md task 4.4) that a
  from-scratch control needs no special-casing for this.
- Add a focus-accent color (and the strip's dim/selected colors) to `Theme.cs`; `ManagementTabs`
  assigns them onto its inherited `TabbedView` properties (which keep their own generic,
  `lazynats`-independent defaults) rather than `TabbedView` reading `Theme.cs` itself — mirrors how
  `EditableBackground` is already centralized. Delivers the frame-highlight behavior
  `doc/focus-accent-color.md` originally set out to build.
- Remove `src/lazynats.Spike.Tabs` once its `TabbedView` is copied into `src/lazynats` — its
  findings remain captured in the archived spike change, so nothing is lost by deleting the
  duplicate standalone project.
- Horizontal overflow scrolling (spike tasks 6–7) stays unimplemented: the real app's five tab
  captions (`Subscribe`/`Streams`/`Values`/`Objects`/`Templates`) comfortably fit under 80
  columns per the spike's own findings, so this is deferred as explicit follow-up work rather than
  built speculatively.

## Capabilities

### New Capabilities
- `tab-strip-rendering`: the management tab strip's visual contract — a single continuous
  bordered box whose top border carries every tab's caption (carved out via `LineCanvas.Exclude`,
  separated by `│` with an outer `┤...├` tee pair), mouse-click tab selection against that same
  caption layout, and focus-accent coloring (selected caption highlighted, frame/other captions
  accented while the strip has keyboard focus, dim otherwise).

### Modified Capabilities
- `color-theme`: adds a requirement that the tab strip's focus-accent/dim/selected colors are
  centrally declared in `Theme.cs`, not hardcoded on the control, matching the existing
  `EditableBackground` precedent.

## Impact

- `src/lazynats/Components/ManagementTabs.cs`: reimplemented on top of the new `TabbedView`
  instead of `Tabs`.
- `src/lazynats/Components/TabbedView.cs` (new): copied from
  `src/lazynats.Spike.Tabs/TabbedView.cs`, kept generic (Terminal.Gui-only, no `lazynats`
  dependency).
- `src/lazynats/MainWindow.cs`: tab registration switches from setting `Title` on each tab content
  view to passing captions explicitly to `ManagementTabs`.
- `src/lazynats/Theme.cs`: gains the tab strip's accent/dim/selected colors.
- `src/lazynats.Spike.Tabs/` and `lazynats.sln` reference (it was never added to the solution):
  removed.
- No change to NATS/JetStream integration, tab content components (`SubscribeTab`, `StreamsTab`,
  etc.), or `IShortcutSource`/`ShortcutAggregator` wiring — those are unaffected by the container
  swap.
