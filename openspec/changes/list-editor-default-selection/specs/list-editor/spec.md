## ADDED Requirements

### Requirement: Selection Recovers When the Item Collection Changes
The list editor SHALL ensure its list has a valid selected item whenever its item collection is
non-empty. If the underlying list view's selection is absent or refers to an index no longer
present after the item collection changes, the list editor SHALL select the first item.

#### Scenario: The first item added to an empty list becomes selected
- **WHEN** the item collection transitions from empty to containing one or more items and the list
  view has no selection
- **THEN** the first item becomes selected

#### Scenario: A collection rebuild that discards the prior selection re-selects the first item
- **WHEN** the item collection is replaced wholesale (e.g. cleared and repopulated by a subclass
  whose true source of truth lives elsewhere) and the list view's selection becomes invalid as a
  result
- **THEN** the first item in the rebuilt collection becomes selected, provided the collection is
  non-empty

#### Scenario: An existing valid selection is left unchanged
- **WHEN** the item collection changes but the list view's current selection still refers to a
  valid item
- **THEN** the selection is not altered
