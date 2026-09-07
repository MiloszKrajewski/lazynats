## MODIFIED Requirements

### Requirement: Empty-State Hint
A drillable list SHALL, while its item collection is empty, display a non-interactive hint line
in place of the (otherwise blank) list content, using text supplied by each subclass. The hint's
visual style SHALL reflect whether the list currently holds keyboard focus, matching the focused/
unfocused distinction used elsewhere in the app's list components. A subclass's hint text SHALL
mention every list-level operation actually available while the list is empty: refresh, always,
plus creation whenever the subclass has creation enabled (`EnableCreate()`).

#### Scenario: Empty collection shows the subclass's hint text
- **WHEN** a drillable list is displayed and its item collection is empty
- **THEN** a hint line supplied by the subclass is shown in place of the list content

#### Scenario: A non-empty collection hides the hint
- **WHEN** the item collection is non-empty
- **THEN** no hint line is shown and the list's items are visible instead

#### Scenario: A creation-enabled list's hint mentions both refresh and creation
- **WHEN** a drillable list has creation enabled and its item collection is empty
- **THEN** the displayed hint text mentions both refreshing (`R`) and adding a new item (`N`)
