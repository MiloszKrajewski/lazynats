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

