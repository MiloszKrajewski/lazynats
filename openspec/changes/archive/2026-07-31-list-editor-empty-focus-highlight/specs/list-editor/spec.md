## MODIFIED Requirements

### Requirement: Empty-State Hint
The list editor SHALL, while its item collection is empty, display a non-interactive hint line in
place of the (otherwise blank) list content. The hint SHALL be hidden as soon as the item
collection contains at least one item, and SHALL reappear if the collection becomes empty again.
The hint's visual style SHALL reflect whether the list editor currently holds keyboard focus: a
dim style while unfocused (its prior fixed appearance), and a visually prominent focused style
while the list editor holds focus, clearly distinguishing it from the dim unfocused appearance.

#### Scenario: Empty collection shows the hint
- **WHEN** the list editor is displayed and its item collection is empty
- **THEN** a hint line is shown in place of the list content, and no other list content is visible

#### Scenario: Adding the first item hides the hint
- **WHEN** the item collection transitions from empty to containing one item (e.g. via a
  successful Ctrl+N create)
- **THEN** the hint is hidden and the list shows that item

#### Scenario: Removing the last item shows the hint again
- **WHEN** the item collection transitions from containing items to empty (e.g. via Ctrl+D on the
  last remaining item)
- **THEN** the hint is shown again in place of the now-empty list content

#### Scenario: The hint is not part of list navigation or selection
- **WHEN** the item collection is empty and the hint is displayed
- **THEN** the hint cannot be selected, and Ctrl+N/E/D behave exactly as they do for any other
  empty collection (Ctrl+N invokes the create callback as usual; Ctrl+E and Ctrl+D are no-ops
  because there is no selected item)

#### Scenario: Empty hint is highlighted while the list editor holds focus
- **WHEN** the item collection is empty and keyboard focus is somewhere within the list editor
- **THEN** the hint renders with a visually prominent focused style rather than its dim unfocused
  style

#### Scenario: Empty hint reverts to dim style when focus leaves
- **WHEN** the item collection is empty, the hint is currently shown with the focused style, and
  keyboard focus moves outside the list editor
- **THEN** the hint reverts to its dim, unfocused style

#### Scenario: Focus-driven style respects a configured background
- **WHEN** an explicit background has been set on the list editor via the `Background` property
  and the item collection is empty
- **THEN** both the focused and unfocused hint styles are rendered against that configured
  background, consistent with how the unfocused hint already honors it
