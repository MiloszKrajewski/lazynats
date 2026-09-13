## Context

`ManagementTabs` (`src/lazynats/Components/ManagementTabs.cs`) subclasses Terminal.Gui's `Tabs`
and layers three workarounds on top of it: `FocusOwnHeader`/`FocusOwnContent`/`SwitchTab`
overrides to scope Up/Down/Left/Right to genuine header focus (`Tabs`' own defaults leak
arrow-key handling from unrelated content), and a `Command.Accept`-routed Tab/Shift+Tab rebinding
to work around `Tabs` setting `TabStop = TabBehavior.TabGroup` on itself (documented at length in
the class's own comments — routing through `NextTabStop`/`PreviousTabStop` directly was found to
silently break after the first Shift+Tab of a session). `doc/focus-accent-color.md` separately
records two abandoned attempts to color-accent the focused tab using `Tabs`: whole-border
recoloring broke at tab-frame seams (`LineCanvas`'s intersection resolution has no focus-aware
tie-break), and title-text-only recoloring never found where `Tabs`' header attribute actually
comes from.

The archived `2026-09-10-spike-tab-strip-control` change built and validated a from-scratch
`TabbedView` (`src/lazynats.Spike.Tabs/TabbedView.cs`) against exactly these problems: a single
continuous bordered box (real corners, real side/bottom lines) whose top border carries every
caption, carved out via `LineCanvas.Exclude(Region)`, with native (not `Tabs`-derived) keyboard
and mouse handling. `src/lazynats.Spike.Tabs/FINDINGS.md` records the confirmed API specifics
(which object exposes `Exclude`, draw-order guarantees, the `_headerProxy` focus-proxy pattern,
`CanFocus` propagation rules, `BlurCurrentFocus`'s cycle-safe loop) this design builds on rather
than re-deriving. Rendering, keyboard nav, and mouse selection are all confirmed live; horizontal
overflow scrolling (spike tasks 6–7) was explicitly deferred and never built.

## Goals / Non-Goals

**Goals:**
- Replace `ManagementTabs`' `Tabs` base with `TabbedView`, eliminating the `TabGroup`
  Tab/Shift+Tab workaround entirely (per FINDINGS.md task 4.4, a from-scratch control needs none).
- Preserve `tab-navigation`'s existing keyboard/naming contract exactly (Up/Down header↔content
  toggle, Left/Right switching, Alt+N direct select, `N:Title` captions) — this is a container
  swap, not a behavior change, from that spec's point of view.
- Give the tab strip a focus-accent color, closing the `TODO.md` item ("different frame color for
  active window") and `doc/focus-accent-color.md`'s original goal.
- Centralize the strip's accent/dim/selected colors in `Theme.cs`, matching the existing
  `EditableBackground` convention (`color-theme`'s "single, centrally-declared" pattern) — applied
  by `ManagementTabs`, the `lazynats`-specific subclass, not by `TabbedView` itself.
- Keep `TabbedView` generic: it depends only on Terminal.Gui, never on `Theme`, `Services`, or any
  other `lazynats` component. Every `lazynats`-specific customization lives in `ManagementTabs`.
- Retire `src/lazynats.Spike.Tabs` once its control is copied into `src/lazynats`, avoiding a
  second, drifting copy of the same code.

**Non-Goals:**
- Horizontal overflow scrolling. The real app's five captions (`1:Subscribe`, `2:Streams`,
  `3:Values`, `4:Objects`, `5:Templates`) total well under 80 columns; building scroll-window/cue
  logic now would be speculative. Left as explicit follow-up if the tab count or terminal-width
  assumptions ever change.
- Any change to tab *content* (`SubscribeTab`, `StreamsTab`, etc.) or to `IShortcutSource`/
  `ShortcutAggregator` — the container swap doesn't touch how content is built or how shortcuts
  are discovered.
- AOT/trimming validation — `TabbedView` is plain `View`-derived code with no reflection-heavy
  patterns; nothing here needs `lazynats.AotProbe`-style validation beyond the normal build.

## Decisions

- **Promote by copying, not re-deriving.** Copy `TabbedView.cs` from
  `src/lazynats.Spike.Tabs/TabbedView.cs` into `src/lazynats/Components/TabbedView.cs` near-verbatim
  (namespace change, `internal` visibility kept — it's `Components`-internal like
  `ListEditorView<T>`/`EditFrame`), rather than rewriting from the design doc. The spike's
  FINDINGS.md-documented API specifics (corner-cell exclusion quirk at `X = 1`, `_headerProxy`
  needing `CanFocus` on every ancestor, `BlurCurrentFocus`'s seen-set loop, `App.Navigation.
  FocusedChanged` over per-view `HasFocusChanged`) are exactly the kind of hard-won, version-
  sensitive detail `doc/focus-accent-color.md` already shows is expensive to lose and re-derive.
  The spike source at `src/lazynats.Spike.Tabs/TabbedView.cs` is left untouched until the deletion
  step (Migration Plan step 4) — this is a copy, not a move.
- **`TabbedView` stays generic; `ManagementTabs` applies all `lazynats`-specific customization.**
  `TabbedView`'s `AccentColor`/`DimColor`/`SelectedForegroundColor` keep the spike's own
  `public virtual Color { get; set; }` shape and generic literal defaults
  (`Color.BrightYellow`/`Color.Gray`/`Color.Black`) unchanged — `TabbedView.cs` itself gains no
  reference to `Theme` or any other `lazynats` component, staying a plain Terminal.Gui control
  that could be dropped into an unrelated project as-is. `Theme.cs` still gains
  `TabAccentColor`/`TabDimColor`/`TabSelectedForegroundColor` constants (matching
  `EditableBackground`'s precedent and `color-theme`'s "single, centrally-declared" requirement),
  but it's `ManagementTabs`'s constructor that assigns them onto the inherited properties
  (`AccentColor = Theme.TabAccentColor;` etc.) — the same "customization in the derived class"
  shape `EditFrame`/`ListEditorView<T>` already use for their own injected callbacks/presenters.
- **`ManagementTabs` keeps wrapping, not replacing, its role as the app-facing type.**
  `ManagementTabs` becomes a thin subclass of the new `TabbedView` (mirroring its current
  relationship to `Tabs`), keeping `MainWindow`'s call sites (`tabs.Add(...)`,
  `tabs.SelectTab(...)`) structurally similar. `Add` changes shape: `Tabs.Add(params View[])` read
  each view's own `Title`; `TabbedView.AddTab(caption, content)` takes the caption explicitly.
  `ManagementTabs` gets a matching `Add(string caption, View content)` (or `MainWindow` calls
  `TabbedView.AddTab` directly per tab) — exact shape decided during implementation, but the
  `N:Title` string itself keeps coming from `MainWindow`, unchanged from today's
  `Title = " 1:Subscribe "` values (just passed as a caption argument instead of a property).
- **`SelectTab(View tab)` becomes index- or reference-based against `TabbedView`'s model.**
  `Tabs`' `Value` setter took the content view directly; `TabbedView` is `SelectedIndex`/
  `Select(int)`-based. `ManagementTabs.SelectTab` keeps its existing `View` parameter (no
  `MainWindow` call-site changes beyond `Add`) and resolves the index internally.
  `MainWindow`'s `topLevelShortcuts` Alt+N actions (`tabs.SelectTab(subscribeTab)`, etc.) stay
  as-is.
- **Delete `src/lazynats.Spike.Tabs` in this same change**, not a later cleanup. It was never
  referenced by `lazynats.sln` or `src/lazynats`, so removal has no build-graph impact; keeping it
  around post-adoption would just be a second, unmaintained copy of the same control that could
  silently drift from the real one.

## Risks / Trade-offs

- [Risk] The spike's `internal` visibility and `Components`-namespace fit assumed a standalone
  project; moving it into `src/lazynats/Components/` could collide with existing names (`Theme`,
  `Attribute`/`Color` aliasing already used elsewhere in that folder) → [Mitigation] Diff the
  spike's `using`/alias block against `ManagementTabs.cs`'s own before the move; the spike already
  aliases `Attribute`/`Color` the same way `MainWindow.cs` does, so this is expected to be a
  non-issue, just worth confirming at copy time.
- [Risk] `TabbedView`'s `SelectedIndexChanged` event replaces `Tabs`' `SelectedTabChanged` (used
  nowhere directly today per the current `ManagementTabs.cs`, but worth confirming no other file
  hooks `Tabs`-specific events before deletion) → [Mitigation] Grep `src/lazynats` for `Tabs`-
  specific API usage beyond `ManagementTabs.cs`/`MainWindow.cs` before removing the `Tabs` base,
  as part of the implementation tasks.
- [Risk] Deleting `src/lazynats.Spike.Tabs` loses a working standalone reproduction if a future
  regression needs isolated debugging → [Mitigation] `FINDINGS.md`'s content isn't deleted — it's
  already preserved under the archived `openspec/changes/archive/2026-09-10-spike-tab-strip-
  control/` change, which stays in the repo regardless of the spike project's fate.

## Migration Plan

Single-PR swap, no phased rollout (a desktop-run TUI, not a deployed service):
1. Copy `TabbedView` into `src/lazynats/Components/`, unchanged in behavior and still
   `lazynats`-independent.
2. Add the tab-strip color constants to `Theme.cs`; reimplement `ManagementTabs` on the new base,
   assigning those constants onto its inherited `TabbedView` properties; update `MainWindow`'s
   tab-registration calls.
3. Manually verify via `tmux` (per this repo's established technique) that every
   `tab-navigation`/`tab-scoped-list-shortcuts` scenario still holds against the new control.
4. Delete `src/lazynats.Spike.Tabs`.

No feature flag or rollback path beyond normal `git revert` — this is pure internal UI-component
substitution with no persisted state or external contract.

## Open Questions

- Exact shape of `ManagementTabs.Add`/tab-registration API (a variadic `(caption, content)` tuple
  list vs. repeated `AddTab` calls from `MainWindow`) — left to implementation, doesn't affect any
  spec-level behavior.
