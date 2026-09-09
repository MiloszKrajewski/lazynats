## ADDED Requirements

### Requirement: Row Text Uses the App's Identifier Color
A drillable list SHALL render each row's formatted item text (produced by its injected
`IValuePresenter<T>`) in the app's single, shared identifier color (`Theme.SubjectColor`), the same
color already used to highlight an identifier elsewhere in the app (a message's subject in the
live feed and message detail dialog, a KV key in its value detail dialog), rather than in
unstyled/plain text. This coloring applies uniformly to every `DrillableListView<T>` subclass and
is not configurable per subclass.

#### Scenario: A drillable list's rows render in the identifier color
- **WHEN** a `DrillableListView<T>` subclass (e.g. the stream list, consumer list, a KV/OBJ bucket
  list, the key list, the object list, or the template list) renders its rows
- **THEN** each row's item text is drawn in `Theme.SubjectColor`

#### Scenario: A highlighted row's identifier color composes with its selection background
- **WHEN** a row in a drillable list is currently highlighted/selected
- **THEN** the row's text still renders in `Theme.SubjectColor`, composed with whichever background
  the list's selection highlight already applies to that row - the identifier color changes only
  the foreground, never overriding the selection background

#### Scenario: A list-editor-based list is unaffected by this requirement
- **WHEN** a `ListEditorView<T>`-based list (e.g. the subscriptions list or the header editor)
  renders its rows
- **THEN** its row text is unaffected by *this* requirement - this requirement applies only to
  `DrillableListView<T>` subclasses; a `ListEditorView<T>`-based list's own row coloring, if any,
  is governed instead by `list-editor`'s "Per-Subclass Row Text Color" (e.g. the subscriptions
  list opts in there, the header editor doesn't)
