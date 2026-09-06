## ADDED Requirements

### Requirement: Empty-State Hint
The list editor SHALL, while its item collection is empty, display a dim, non-interactive hint
line in place of the (otherwise blank) list content. The hint SHALL be hidden as soon as the item
collection contains at least one item, and SHALL reappear if the collection becomes empty again.

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

### Requirement: Per-Subclass Hint Text
The list editor SHALL obtain its empty-state hint text from an overridable source, so each
subclass can supply wording appropriate to its item type; subclasses that do not override it SHALL
still display a generic, non-empty hint rather than no hint at all.

#### Scenario: A subclass overrides the hint text
- **WHEN** a derived list editor overrides its hint-text source with a message specific to its item
  type
- **THEN** the empty-state hint displays that message

#### Scenario: A subclass does not override the hint text
- **WHEN** a derived list editor does not override its hint-text source
- **THEN** the empty-state hint displays a generic, non-empty default message
