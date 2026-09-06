## ADDED Requirements

### Requirement: No In-View Header
The live feed view SHALL NOT render its own heading text or divider line; it SHALL rely on its
host container's own title and border for framing, so that the view never duplicates a title
already shown by whatever it is hosted in.

#### Scenario: Feed view renders without a redundant heading
- **WHEN** the live feed view is displayed inside its host frame (titled "Live Feed")
- **THEN** the view shows no additional heading text or divider line of its own, and its message
  list starts at the top row of the view's content area
