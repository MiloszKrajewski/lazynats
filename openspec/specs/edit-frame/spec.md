# edit-frame Specification

## Purpose

`EditFrame` is a lightweight, non-focusable wrapper view that gives a single wrapped editing
control (e.g. a text field or list editor) a padded, bordered-looking presentation without
introducing a distinct focus stop of its own. It recolors its rule based on the wrapped child's
focus state, or a caller-supplied override, and renders using explicitly configured background
colors rather than introspecting the wrapped child's scheme.

## Requirements

### Requirement: Pass-Through Focus
`EditFrame` SHALL have no interactive content of its own, so that keyboard and mouse focus
navigation (Tab/Shift+Tab, mouse click) always resolves to `EditFrame`'s single wrapped child view
via normal focus drill-down, never stopping at the frame itself as a distinct destination.

#### Scenario: Tab navigation reaches the wrapped child directly
- **WHEN** the user tabs through a view containing an `EditFrame`-wrapped field
- **THEN** keyboard focus lands on the wrapped child, with no separate, distinct tab stop for the
  frame itself observable to the user

### Requirement: Single-Child Layout
`EditFrame` SHALL host exactly one child view, reserving exactly one row above and one row below
that child for its top/bottom rule, exactly two columns to the left of that child (its outer
margin and inner padding columns), and exactly one column to its right (its margin column).

#### Scenario: Wrapping a child adds two rows and three columns total
- **WHEN** a child view of a given width and height is wrapped in an `EditFrame`
- **THEN** the resulting `EditFrame` occupies the child's height plus exactly two additional rows
  (one above, one below) and the child's width plus exactly three additional columns (two to the
  left, one to the right)

### Requirement: Focus-Driven Recolor
`EditFrame` SHALL redraw its rule using `InnerBackgroundFocused` whenever its wrapped child
currently has focus, and `InnerBackgroundNormal` otherwise, updating automatically in response to
the child's own focus-changed notification without requiring any external notification from the
frame's caller. This behavior applies only while `InnerBackgroundOverride` is unset (see the
Caller-Supplied Background Override requirement).

#### Scenario: Child gaining focus recolors the rule
- **WHEN** the wrapped child transitions from not having focus to having focus, and no override is
  set
- **THEN** the frame's rule is redrawn using `InnerBackgroundFocused`

#### Scenario: Child losing focus reverts the rule
- **WHEN** the wrapped child transitions from having focus to not having focus, and no override is
  set
- **THEN** the frame's rule is redrawn using `InnerBackgroundNormal`

### Requirement: Explicit Background Configuration
`EditFrame` SHALL derive its rule colors solely from explicitly configured colors —
`OuterBackground`, `InnerBackgroundNormal`, `InnerBackgroundFocused`, and the optional
`InnerBackgroundOverride` — and SHALL NOT derive any of them by introspecting the wrapped child's
scheme or semantic visual role. `OuterBackground` SHALL default to the color inherited from
`EditFrame`'s own SuperView unless explicitly overridden.

#### Scenario: OuterBackground defaults to the inherited ambient background
- **WHEN** an `EditFrame` is constructed without an explicit `OuterBackground`
- **THEN** it renders using the background inherited from its SuperView, consistent with normal
  scheme-inheritance behavior for a view with no explicit scheme of its own

#### Scenario: Caller overrides OuterBackground explicitly
- **WHEN** an `EditFrame` is constructed with an explicit `OuterBackground` value
- **THEN** it renders using that value regardless of what its SuperView's background is

### Requirement: Caller-Supplied Background Override
`EditFrame` SHALL expose a nullable `InnerBackgroundOverride` color, defaulting to unset. Whenever
it is set to a non-null value, `EditFrame` SHALL use it for the rule's inner color regardless of
the wrapped child's current focus state, taking precedence over both `InnerBackgroundNormal` and
`InnerBackgroundFocused`. Whenever it is unset (`null`), `EditFrame` SHALL fall back to the
focus-driven behavior exactly as if the property did not exist. `EditFrame` SHALL attach no
meaning of its own to why a caller sets or clears this value (e.g. validity, disabled state) —
that judgment belongs entirely to the caller.

#### Scenario: A set override wins regardless of focus
- **WHEN** `InnerBackgroundOverride` is set to a color, whether or not the wrapped child currently
  has focus
- **THEN** the frame's rule is redrawn using the override color, not `InnerBackgroundNormal` or
  `InnerBackgroundFocused`

#### Scenario: Clearing the override restores focus-driven coloring
- **WHEN** `InnerBackgroundOverride` is set back to `null`
- **THEN** the frame's rule immediately reflects the wrapped child's current focus state via
  `InnerBackgroundNormal`/`InnerBackgroundFocused`, as if the override had never been set

### Requirement: Full Padded-Frame Rendering
`EditFrame` SHALL render its frame from the positions and glyphs documented in `doc/glyphs.md`:
a top rule (`TOP`), a bottom rule (`BOT`), a two-column left margin (`LM` outer, `LP` inner) down
every content row, a one-column right margin (`RM`) down every content row, and two corner cells
(`TL`, `BL`) at the left ends of the top and bottom rules — with no distinct top-right or
bottom-right corner glyph; `TOP`/`BOT` simply continue their rule glyph into those cells. Every
position SHALL use `OuterBackground` for its glyph's unfilled portion. `TOP`, `BOT`, `LP`, and `RM`
SHALL ink their glyph's filled portion with the currently active inner background color
(`InnerBackgroundOverride` if set, otherwise `InnerBackgroundNormal`/`InnerBackgroundFocused` per
focus state); `TL`, `BL`, and `LM` SHALL instead ink theirs with the edge accent color (see the
Edge Accent Color requirement).

#### Scenario: Rule and margin rendering uses inner color for the filled portion
- **WHEN** `EditFrame` draws `TOP`, `BOT`, `LP`, or `RM`
- **THEN** that glyph's filled portion appears in the currently active inner background color and
  its unfilled portion appears in `OuterBackground`

#### Scenario: No distinct top-right or bottom-right corner glyph
- **WHEN** `EditFrame` draws its top or bottom rule
- **THEN** the rightmost cell of that rule uses the same glyph as the rest of the rule, not a
  separate corner glyph

### Requirement: Edge Accent Color
`EditFrame` SHALL ink the `TL`, `BL`, and `LM` positions' filled portion with a distinct edge
accent color, independent of the currently active inner background color used elsewhere in the
frame, so the frame's outermost left edge reads as a deliberate accent line rather than blending
into the rest of the frame.

#### Scenario: Left edge accent is visually distinct from the inner background
- **WHEN** `EditFrame` draws `TL`, `BL`, or `LM`
- **THEN** that glyph's filled portion appears in the edge accent color, not in
  `InnerBackgroundNormal`, `InnerBackgroundFocused`, or `InnerBackgroundOverride`
