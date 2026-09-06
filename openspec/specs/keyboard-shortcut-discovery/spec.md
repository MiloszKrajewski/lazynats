# keyboard-shortcut-discovery Specification

## Purpose
TBD - created by archiving change add-list-editor-shortcuts. Update Purpose after archive.
## Requirements
### Requirement: Opt-In Shortcut Advertisement
A view SHALL be able to declare which of its own keyboard shortcuts should be discoverable by
implementing a dedicated shortcut-source contract, exposing a curated list of shortcuts distinct
from the full set of key bindings it may otherwise register for internal or navigational purposes.

#### Scenario: A view with internal key bindings advertises only a subset
- **WHEN** a view has both internal key bindings not meant for discovery (e.g. list navigation)
  and a small set of bindings implementing the shortcut-source contract
- **THEN** only the bindings exposed through the shortcut-source contract are eligible to be
  surfaced, and the internal-only bindings are not

### Requirement: Aggregation Follows the Focus Chain
The system SHALL determine the currently available shortcuts by starting at the currently focused
view and walking up through its ancestors to the root, collecting advertised shortcuts from every
ancestor (including the focused view itself) that implements the shortcut-source contract.

#### Scenario: Shortcuts from an ancestor are available while a descendant has focus
- **WHEN** keyboard focus is on a child control of a view that implements the shortcut-source
  contract
- **THEN** the shortcuts advertised by that ancestor view are included in the currently available
  set

#### Scenario: Shortcuts from an unrelated view are not available
- **WHEN** keyboard focus is within one view's subtree
- **THEN** shortcuts advertised by a different view that is not an ancestor of the focused view are
  not included in the currently available set

### Requirement: Available Shortcuts Update on Focus Change
The system SHALL recompute the currently available set of shortcuts whenever keyboard focus moves
to a different view anywhere in the application.

#### Scenario: Moving focus changes the available shortcuts
- **WHEN** keyboard focus moves from one view to another view with a different set of ancestor
  shortcut sources
- **THEN** the currently available set of shortcuts is recomputed to reflect the newly focused
  view's ancestor chain

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

