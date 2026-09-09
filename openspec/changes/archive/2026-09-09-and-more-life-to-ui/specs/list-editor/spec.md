## ADDED Requirements

### Requirement: Per-Subclass Row Text Color
The list editor SHALL expose an optional row text color, defaulted off, that a subclass may opt
into so its rows render in the app's single, shared identifier color (`Theme.SubjectColor`) -
the same coloring `drillable-list` applies uniformly to every `DrillableListView<T>` subclass -
instead of unstyled/plain text. Unlike `drillable-list`, this is a per-subclass opt-in, not
applied uniformly to every list editor: a subclass whose rows are a pure identifier (e.g. the
subscriptions list, each row a single subject pattern) opts in, while a subclass whose rows are
not a single identifier (e.g. the header editor, each row a key/value pair) does not.

#### Scenario: A subclass that opts in renders its rows in the identifier color
- **WHEN** a list editor subclass (e.g. the subscriptions list) opts into the row text color
- **THEN** each row's item text is drawn in `Theme.SubjectColor`

#### Scenario: A highlighted row's identifier color composes with its selection background
- **WHEN** a row in a list editor that has opted into the row text color is currently
  highlighted/selected
- **THEN** the row's text still renders in `Theme.SubjectColor`, composed with whichever
  background the list's selection highlight already applies to that row - the identifier color
  changes only the foreground, never overriding the selection background

#### Scenario: A subclass that does not opt in is unaffected
- **WHEN** a list editor subclass (e.g. the header editor) does not opt into the row text color
- **THEN** its row text continues to render as plain, unstyled text
