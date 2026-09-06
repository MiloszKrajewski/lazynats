## ADDED Requirements

### Requirement: Modal dialog content top spacing
A `Dialog<T>` subclass that has a button row SHALL give its content area one blank row between
the dialog's title/border and its first field or control, matching the blank row that already
separates the last field from the button row — so its content reads as symmetrically sparse
rather than cramped at the top and airy at the bottom. A `Dialog<T>` subclass with no button row
(a single-field dialog that commits on Enter) is exempt: with no bottom blank row to balance
against, it stays compact rather than gaining unmatched top padding.

#### Scenario: A multi-field create dialog has a blank row above its first label
- **WHEN** `CreateStreamDialog`, `CreateConsumerDialog`, or `CreateBucketDialog` is opened
- **THEN** one blank row separates the dialog's top border from the "Name" label, matching the
  blank row already present above the button row

#### Scenario: A single-field, button-less dialog stays compact
- **WHEN** `PatternDialog` or `HeaderDialog` is opened
- **THEN** no blank row is added above its field's label — the dialog remains as compact
  vertically as it is today
