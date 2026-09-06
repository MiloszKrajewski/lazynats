## ADDED Requirements

### Requirement: Aggregated Shortcuts Are Rendered in the Status Bar
The system SHALL render the currently-available set of shortcuts, as computed by shortcut
aggregation, in the application's status bar, alongside the status bar's fixed/global shortcuts.

#### Scenario: A view's advertised shortcuts appear in the status bar
- **WHEN** keyboard focus is within a view (or one of its descendants) that implements the
  shortcut-source contract
- **THEN** that view's advertised shortcuts are visible in the status bar

#### Scenario: Status bar shortcuts update when the available set changes without a focus change
- **WHEN** the currently-focused view's advertised shortcuts change (e.g. because the view's
  internal state changed) without keyboard focus moving to a different view
- **THEN** the status bar's shortcut display is refreshed to reflect the new set
