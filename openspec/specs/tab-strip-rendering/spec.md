# tab-strip-rendering Specification

## Purpose

The management tab strip (`TabbedView`) renders the app's top-level tabs as a single continuous
bordered box whose top border line carries every tab's caption, with coloring that reflects
selection and keyboard focus state, and supports selecting a tab by clicking its caption.

## Requirements

### Requirement: Single Continuous Bordered Tab Strip
The management tab strip SHALL render as one continuous bordered box (real corners, real
left/right/bottom border lines) whose top border line carries every tab's caption, rather than as
separate bordered frames per tab. Captions SHALL appear in left-to-right tab order, each
separated from its neighbor by a single `│` glyph, with the whole caption run bracketed by one
outer `┤`/`├` tee pair (not a tee pair per caption).

#### Scenario: Multiple tabs render inside one bordered box
- **WHEN** the management tab strip is displayed with its full set of tabs
- **THEN** the top border shows every tab's caption in order, separated by `│`, inside a single
  `┤...├`-bracketed run, with unbroken left/right/bottom border lines forming one continuous box
  around the whole strip

#### Scenario: Border corners and lines are unaffected by caption placement
- **WHEN** captions are rendered into the top border, including a caption near the left or right
  edge of the strip
- **THEN** the box's four corners and its left/right/bottom border lines render as ordinary,
  unbroken lines and corners, with no stray or mismatched glyph adjacent to any caption

### Requirement: Selected and Focus-Accented Caption Coloring
The tab strip SHALL render its selected tab's caption in a visually distinct attribute from every
other caption, and SHALL render the strip with an accent color when keyboard focus is anywhere
within it (its own header representation, or any tab's content), reverting to non-accented
coloring when focus is elsewhere in the application. The selected tab's caption SHALL use a solid,
inverted highlight (foreground/background swapped) whenever keyboard focus is anywhere within the
strip (header or content), not only while the header itself has focus. While the strip's own
header representation specifically has focus, every non-selected caption SHALL additionally render
in the strip's plain (non-inverted) accent color, previewing which tabs Left/Right would switch
to; outside that narrower state, non-selected captions render dim regardless of whether focus is
elsewhere in the strip's content or outside the strip entirely.

#### Scenario: Selected tab is visually distinct from unselected tabs
- **WHEN** the tab strip is displayed with one tab selected
- **THEN** that tab's caption is rendered with a different foreground/background attribute than
  every other tab's caption

#### Scenario: Strip shows an accent color while it holds keyboard focus
- **WHEN** keyboard focus is within the tab strip (its header, or any tab's content)
- **THEN** the strip's border renders in its accent color

#### Scenario: Strip reverts to non-accented coloring when focus leaves it
- **WHEN** keyboard focus moves outside the tab strip entirely
- **THEN** the strip's border no longer renders in its accent color

#### Scenario: Selected tab's caption is solidly highlighted whenever the strip has focus
- **WHEN** keyboard focus is anywhere within the strip (its own header representation, or any
  tab's content) and a tab is selected
- **THEN** that tab's caption renders with a solid, inverted (foreground/background-swapped)
  highlight

#### Scenario: Other captions preview Left/Right targets only while the header itself has focus
- **WHEN** the strip's own header representation (not a tab's content) holds keyboard focus
- **THEN** every non-selected caption renders in the strip's plain accent color, distinct from
  both the selected tab's solid highlight and the dim color those captions use otherwise

#### Scenario: Other captions stay dim while a tab's content holds focus
- **WHEN** keyboard focus is within a tab's content (not the strip's own header representation)
- **THEN** every non-selected caption renders dim, the same as when focus is outside the strip
  entirely

### Requirement: Mouse Tab Selection
The tab strip SHALL support selecting a tab by clicking its caption, hit-tested against the same
caption layout used for rendering, and SHALL move focus into that tab's content as part of the
same click.

#### Scenario: Clicking an unselected tab's caption selects it
- **WHEN** the user clicks within the rendered rectangle of an unselected tab's caption
- **THEN** that tab becomes the selected tab and its content receives keyboard focus
