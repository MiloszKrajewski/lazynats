## ADDED Requirements

### Requirement: Configurable Background
The list editor SHALL expose an explicit background color, independent of any implicitly
inherited scheme, so it can be visually paired consistently with other edit controls (e.g. when
wrapped in a padded `EditFrame`).

#### Scenario: Setting a background applies it to the list's fill
- **WHEN** a background color is set on the list editor
- **THEN** the list's fill is rendered using that color rather than an inherited scheme color

#### Scenario: Background left unset falls back to inherited behavior
- **WHEN** no explicit background is set on the list editor
- **THEN** the list editor renders using its previously inherited scheme background, unchanged
  from prior behavior
