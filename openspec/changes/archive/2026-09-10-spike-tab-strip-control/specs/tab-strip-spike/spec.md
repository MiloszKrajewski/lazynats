## ADDED Requirements

### Requirement: Standalone Project Isolation
The spike SHALL live in its own project, buildable and runnable independently of `src/lazynats`
and `lazynats.sln`, referencing the same `Terminal.Gui` version (`2.4.17`) as the main app.

#### Scenario: Building the spike alone
- **WHEN** `dotnet build src/lazynats.Spike.Tabs` is run without building `lazynats.sln`
- **THEN** the build succeeds without requiring any project under `src/lazynats` or NATS
  connectivity

#### Scenario: Terminal.Gui version matches the main app
- **WHEN** `src/lazynats.Spike.Tabs`'s package references are inspected
- **THEN** it references `Terminal.Gui` `2.4.17`, the same package and version as
  `src/lazynats.csproj`

### Requirement: Bordered Tab Strip Rendering
The tab strip control SHALL render as a single continuous bordered box (real corners, real
left/right/bottom lines) whose top edge carries every tab's caption cut directly into the border
line, with the selected tab's caption rendered in a visually distinct attribute (foreground and
background) from unselected captions.

#### Scenario: Multiple placeholder tabs rendered
- **WHEN** the spike app starts with several placeholder tabs added to the control
- **THEN** the top border shows each tab's caption in order, separated by vertical bars, inside
  the strip's own left/right/top border run

#### Scenario: Selected tab is visually distinct
- **WHEN** a tab is the currently selected one
- **THEN** that tab's caption is drawn with a different foreground/background attribute than every
  other caption, confirmed via `tmux capture-pane -p -e` reading the actual rendered ANSI
  attributes (not just characters)

### Requirement: Border Auto-Join Around Caption Cutouts
The control SHALL carve each caption's rectangle out of the container border's `LineCanvas` output
via `LineCanvas.Exclude(Region)` rather than compositing separate bordered per-tab frames, so the
border's corners and side lines resolve correctly on either side of every caption with no visible
seam artifact.

#### Scenario: Corners and side lines are unaffected by caption cutouts
- **WHEN** the tab strip is rendered with captions of varying widths, including at least one
  caption near the left or right edge of the strip
- **THEN** the box's four corners and its left/right/bottom border lines render as ordinary
  unbroken lines/corners, with no stray or mismatched glyph at any point adjacent to a caption

#### Scenario: Custom-painted captions are not overwritten
- **WHEN** a caption is painted with a per-tab attribute during content drawing
- **THEN** the painted caption text and attribute remain visible after the frame's border-line
  render pass completes, unmodified by it

### Requirement: Native Keyboard Navigation
The control SHALL implement tab-strip keyboard navigation directly (no `Tabs`/`ManagementTabs`
base class): Up moves focus from a tab's content into its own header; Down moves focus from a
focused header back into that tab's content; Left/Right switch the selected tab while a header is
focused; a direct-select shortcut (e.g. Alt+N) selects a tab by position from anywhere.

#### Scenario: Header/content focus toggle
- **WHEN** focus is inside a tab's content and Up is pressed, followed by Down
- **THEN** focus moves to that tab's own header on Up, then back into the tab's content on Down

#### Scenario: Switching tabs from a focused header
- **WHEN** a tab's header is focused and Left or Right is pressed
- **THEN** the selection moves to the previous or next tab respectively, wrapping at either end

#### Scenario: Direct selection by shortcut
- **WHEN** the direct-select shortcut for a given tab position is pressed from anywhere in the
  app
- **THEN** that tab becomes selected and its default focusable content receives focus

### Requirement: Mouse Tab Selection
The control SHALL support selecting a tab by clicking its caption, using the same
caption-rectangle layout data the control uses for drawing and for `LineCanvas.Exclude`.

#### Scenario: Clicking an unselected tab's caption
- **WHEN** the user clicks within the screen rectangle of an unselected tab's caption
- **THEN** that tab becomes the selected tab

### Requirement: Horizontal Overflow Scrolling
When the full set of tab captions does not fit within the strip's current width, the control
SHALL show a scrolled window over the captions that never renders a partial caption, SHALL
auto-scroll to keep the selected tab fully visible whenever selection changes, and SHALL indicate
when additional tabs exist off-screen to the left or right.

#### Scenario: Narrow width hides some captions without truncating any
- **WHEN** the strip is narrower than the combined width of all tab captions
- **THEN** only whole captions are shown; no caption is cut off mid-caption at either visible edge

#### Scenario: Selecting an off-screen tab scrolls it into view
- **WHEN** a tab outside the currently visible window is selected (via keyboard shortcut or click
  on a visible edge control)
- **THEN** the visible window scrolls so that tab's caption becomes fully visible

#### Scenario: Off-screen tabs are indicated
- **WHEN** one or more tabs exist beyond the left or right edge of the currently visible window
- **THEN** the strip shows a visible cue at that edge indicating more tabs are available in that
  direction
