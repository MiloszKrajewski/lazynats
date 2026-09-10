## 1. Project Setup

- [x] 1.1 Create `src/lazynats.Spike.Tabs` as a new SDK-style console project (`net10.0`,
      `OutputType Exe`, `ImplicitUsings`/`Nullable` enabled), not added to `lazynats.sln` and not
      referencing `src/lazynats`.
- [x] 1.2 Add a `PackageReference` for `Terminal.Gui` `2.4.17` (matching `src/lazynats.csproj`
      exactly — not `AotProbe`'s `Terminal.Gui.Editor` `2.5.7`).
- [x] 1.3 Add a minimal `Program.cs` that initializes the Terminal.Gui `Application`/`App` and
      runs an empty `Toplevel`, confirming the project builds and launches standalone.

## 2. Isolate and Confirm `LineCanvas.Exclude`

- [x] 2.1 In a throwaway `View` (not yet the real control), draw a full `Border` and identify,
      live, which object actually exposes the `LineCanvas` to call `Exclude` on in 2.4.17
      (`View.Border`, `Border.GetOrCreateView()`, or elsewhere) — do not assume the docs snippet's
      object without confirming against this installed version.
- [x] 2.2 Call `Exclude(Region)` for a single rectangle in the middle of the top border line;
      confirm via `tmux capture-pane -p -e` that the border still auto-joins correctly on both
      sides of the excluded rectangle (no stray corner/T-junction glyph).
- [x] 2.3 Paint arbitrary text with a distinct `SetAttribute` into the excluded rectangle during
      content drawing; confirm it renders and survives un-overwritten after the frame's
      line-canvas render pass, both via `tmux capture-pane -p -e` (logical attribute) and outside
      tmux in a real terminal (actual displayed color — tmux/psmux under-reports color-only cell
      changes, a gotcha already documented in `doc/focus-accent-color.md`).
- [x] 2.4 Record what was actually found in this step (object/API used, any surprises vs. the
      design doc's assumption) as a short note in this project, so a later `lazynats` adoption
      change doesn't have to re-derive it.

## 3. Tab Strip Control: Layout and Rendering

- [x] 3.1 Create the `TabbedView` `View` subclass with an `AddTab(string caption, View content)`
      method that stores `(caption, content)` pairs and adds `content` as a permanent, initially
      hidden SubView.
- [x] 3.2 Implement layout: given the current width and the full caption list, compute each
      caption's rectangle (for drawing, `Exclude`, and later hit-testing) and the strip's
      full-box border.
- [x] 3.3 Implement rendering: full `Border` for the box, `Exclude` per caption rectangle, then
      paint each caption's text with a distinct attribute for the selected vs. unselected tab.
- [x] 3.4 Wire selection state: selecting a tab shows its content (`Visible = true`) and hides all
      others, mirroring `DrillableListView`/`PollingDetailsView`'s existing `Visible`-toggle
      pattern.
- [x] 3.5 Add several placeholder content views (plain labels are enough) and confirm multi-tab
      rendering matches the design's sketch, selected tab visually distinct, via
      `tmux capture-pane -p -e`.

## 4. Keyboard Navigation

- [x] 4.1 Implement Up/Down: Up moves focus from a tab's content to the strip's own header
      representation for that tab; Down moves focus back into the tab's content.
- [x] 4.2 Implement Left/Right: while the header is focused, switches the selected tab
      (wrapping at either end).
- [x] 4.3 Implement a direct-select shortcut (e.g. Alt+1..N) that selects a tab by position from
      anywhere in the app and focuses its default focusable content.
- [x] 4.4 Determine and verify, live, whatever Tab/Shift+Tab behavior this control needs — do not
      assume `ManagementTabs.cs`'s `TabGroup`-routing workaround still applies to a from-scratch
      control; confirm whichever `TabBehavior` is chosen actually behaves as expected.
- [x] 4.5 Verify focus-driven visual state (if any at this stage) is wired through
      `App.Navigation.FocusedChanged`, not per-view `HasFocusChanged`, per the documented modal
      `Dialog` lesson in `doc/focus-accent-color.md`.

## 5. Mouse Selection

- [x] 5.1 Handle a mouse click on the strip, translating the click's viewport-relative position
      against the same caption-rectangle table used for drawing/`Exclude`.
- [x] 5.2 Clicking an unselected tab's caption selects it (same effect as the keyboard path).

## 6. Horizontal Overflow Scrolling

- [ ] 6.1 Add a scroll offset (in whole tabs) to the layout computation from 3.2, recomputed on
      resize and on selection change.
- [ ] 6.2 Render only the visible window of captions, never cutting one mid-word; reserve edge
      cue glyphs (e.g. `<`/`>`) in place of filler dashes when tabs exist off-screen in that
      direction.
- [ ] 6.3 Auto-scroll so a newly selected tab (via keyboard or click) is always fully visible.
- [ ] 6.4 Verify with a narrow terminal width (fewer columns than the full caption list) via tmux,
      confirming no caption is ever partially shown.

## 7. Wrap-Up

- [ ] 7.1 Do a full interactive pass (not just tmux) confirming rendering, keyboard nav, mouse
      click, and overflow scrolling all work together in one running session.
- [ ] 7.2 Summarize findings (what worked as designed, what surprised, any API specifics future
      adoption work will need) for use when proposing adoption into `src/lazynats`.
