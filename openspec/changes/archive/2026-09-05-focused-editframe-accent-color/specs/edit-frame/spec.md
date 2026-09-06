## MODIFIED Requirements

### Requirement: Edge Accent Color
`EditFrame` SHALL ink the `TL`, `BL`, and `LM` positions' filled portion with a distinct edge
accent color, independent of the currently active inner background color used elsewhere in the
frame, so the frame's outermost left edge reads as a deliberate accent line rather than blending
into the rest of the frame. `EditFrame` SHALL select between two edge accent colors based on the
wrapped child's current focus state — `EdgeAccentFocused` while the wrapped child has focus, and
`EdgeAccent` otherwise — mirroring how `InnerBackgroundFocused`/`InnerBackgroundNormal` are
selected, and updating automatically in response to the child's own focus-changed notification.
`EdgeAccent` SHALL default to white and `EdgeAccentFocused` SHALL default to a distinct color when
the caller leaves either unset, so every existing `EditFrame` gains a focus-driven accent without
requiring any caller to set either property explicitly.

#### Scenario: Left edge accent is visually distinct from the inner background
- **WHEN** `EditFrame` draws `TL`, `BL`, or `LM`
- **THEN** that glyph's filled portion appears in the currently selected edge accent color, not in
  `InnerBackgroundNormal`, `InnerBackgroundFocused`, or `InnerBackgroundOverride`

#### Scenario: Focused wrapped child uses the focused edge accent color
- **WHEN** the wrapped child currently has focus, and the caller has not overridden
  `EdgeAccentFocused`
- **THEN** `TL`, `BL`, and `LM` are inked with the default focused edge accent color, distinct from
  the default unfocused edge accent color

#### Scenario: Unfocused wrapped child uses the normal edge accent color
- **WHEN** the wrapped child does not currently have focus
- **THEN** `TL`, `BL`, and `LM` are inked with `EdgeAccent` (default white), matching this
  requirement's pre-existing behavior

#### Scenario: Child gaining or losing focus recolors the edge accent
- **WHEN** the wrapped child's focus state changes
- **THEN** `EditFrame` redraws `TL`, `BL`, and `LM` using the newly selected edge accent color, with
  no external notification required from the frame's caller beyond the child's own
  focus-changed notification

#### Scenario: Caller overrides either edge accent color explicitly
- **WHEN** a caller sets `EdgeAccent` and/or `EdgeAccentFocused` to an explicit color
- **THEN** `EditFrame` uses that explicit color for the corresponding focus state instead of the
  default
