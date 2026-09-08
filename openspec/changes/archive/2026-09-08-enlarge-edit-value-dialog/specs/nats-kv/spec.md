## ADDED Requirements

### Requirement: Create/Edit Key Dialog Sizing
The create-key/edit-key dialog (opened via N or E per "Create Key"/"Edit Key") SHALL size
its Name and Value fields' width, and the Value field's height, responsively to the current
terminal size rather than using a fixed size, up to a maximum cap in each dimension. Below that
cap the dialog SHALL still fit within the terminal.

#### Scenario: Fields widen on a large terminal
- **WHEN** the create-key or edit-key dialog opens on a terminal wide enough to exceed the
  dialog's preferred width cap plus margin
- **THEN** the Name and Value fields render at the dialog's capped preferred width, wider than
  the dialog's previous fixed 43-column width

#### Scenario: Value field grows taller on a tall terminal
- **WHEN** the create-key or edit-key dialog opens on a terminal tall enough to exceed the Value
  field's minimum height plus the dialog's other fixed rows
- **THEN** the Value field renders taller than the dialog's previous fixed 10-row height, up to
  its maximum height cap

#### Scenario: Dialog still fits a small terminal
- **WHEN** the create-key or edit-key dialog opens on a terminal at or near the minimum size the
  application already supports
- **THEN** the Value field renders no smaller than its previous fixed 10-row height, and the
  dialog remains fully visible on-screen

#### Scenario: Name field height is unaffected
- **WHEN** the create-key or edit-key dialog opens on any terminal size
- **THEN** the Name field remains a single line of input, unchanged from today
