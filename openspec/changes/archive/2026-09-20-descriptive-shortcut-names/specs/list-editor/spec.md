## ADDED Requirements

### Requirement: Explicit Operation Labels Required at Construction
Because create, edit, and delete are unconditionally available on every list editor instance
(never selectively activated the way `drillable-list`'s shapes are), the list editor SHALL
require the label text for each of these three operations to be supplied as a required
constructor argument, with no default the base class supplies on a subclass's behalf. The Filter
operation, where activated, SHALL instead require its label as an argument to the same opt-in
activation call `drillable-list`'s Shared Filter Wiring specifies, since Filter genuinely is
opt-in — reused for both the advertised hint and the filter dialog's own title, so the two can
never disagree.

#### Scenario: A list editor's create/edit/delete labels come from construction
- **WHEN** a list editor subclass is constructed with "Add new Header", "Edit Header", and
  "Delete Header" as its create/edit/delete labels
- **THEN** its advertised create operation is labeled exactly "Add new Header", its edit operation
  exactly "Edit Header", and its delete operation exactly "Delete Header"

#### Scenario: A list editor's Filter label, where activated, is supplied at activation
- **WHEN** a list editor subclass activates the shared filter wiring with the label "Filter
  Headers"
- **THEN** its advertised filter operation is labeled exactly "Filter Headers", and opening it
  shows a modal pattern dialog titled exactly "Filter Headers"

#### Scenario: A list editor that does not activate filter wiring has no Filter operation
- **WHEN** a list editor subclass does not activate the shared filter wiring
- **THEN** no filter operation is present among its advertised operations, standalone or
  tab-hosted, and no Filter label is required of it
