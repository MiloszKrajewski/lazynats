## 1. Data source coloring

- [x] 1.1 Add an optional `Color? textColor = null` constructor parameter to
      `PresenterListDataSource<T>` (`src/lazynats/Components/PresenterListDataSource.cs`).
- [x] 1.2 When `textColor` is set, `Render` builds a single-segment `ColoredRow`
      (`new RowSegment(textColor, text)`) from the presenter's formatted text and draws it via
      `ColoredRowRenderer.Render`, instead of the current manual slice/pad/`AddStr` logic; when
      `textColor` is `null`, keep today's exact rendering unchanged.

## 2. Wire up DrillableListView

- [x] 2.1 In `DrillableListView<T>`'s constructor (`src/lazynats/Components/DrillableListView.cs`),
      pass `Theme.SubjectColor` as `PresenterListDataSource<T>`'s `textColor`.
- [x] 2.2 Give `ListEditorView<T>` (`src/lazynats/Components/ListEditorView.cs`) the same optional
      `textColor` constructor parameter (defaulted `null`), passed through to its own
      `PresenterListDataSource<T>`; `SubscriptionsView` opts in with `Theme.SubjectColor`,
      `HeaderEditorView` leaves it unset so it keeps rendering as plain text.

## 3. Verification

- [x] 3.1 Build the app (`dotnet build src/lazynats.sln`) and confirm no warnings/errors from the
      changed files.
- [x] 3.2 Launch the app against a real NATS server and visually check, in a real terminal (not
      `tmux capture-pane`, which drops color), that stream, consumer, KV/OBJ bucket, key, object,
      and template list rows render cyan, both unselected and selected, with acceptable contrast.
      (Confirmed.)
- [x] 3.2b Same check for the subscriptions list (newly in scope) - needs a rebuild/restart of the
      running app to pick up.
- [x] 3.3 Visually confirm the Publish dialog's header editor is unchanged (still plain text).
