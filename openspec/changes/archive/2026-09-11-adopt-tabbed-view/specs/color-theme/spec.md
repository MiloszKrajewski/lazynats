## ADDED Requirements

### Requirement: Tab strip accent colors are centrally declared
The system SHALL declare the management tab strip's focus-accent color, dim (unselected-caption)
color, and selected-caption foreground color in `Theme.cs`, alongside the existing
`EditableBackground` constant, rather than hardcoding them on the tab strip control itself.

#### Scenario: Tab strip reads its colors from Theme.cs
- **WHEN** the management tab strip resolves its accent, dim, or selected-caption foreground color
- **THEN** the resolved value comes from `Theme.cs`'s declared constants, not a literal color
  defined on the tab strip control

#### Scenario: Changing a theme constant changes the tab strip's rendering
- **WHEN** one of `Theme.cs`'s tab-strip color constants is changed
- **THEN** the management tab strip renders using the new value without requiring any change to
  the tab strip control's own code
