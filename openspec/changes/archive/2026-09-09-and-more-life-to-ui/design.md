## Context

`PresenterListDataSource<T>` is the one `IListDataSource` shared by both `DrillableListView<T>`
(streams, consumers, KV/OBJ buckets, keys, objects, templates) and `ListEditorView<T>`
(subscriptions, header editor): it formats each item via an injected `IValuePresenter<T>` and
draws the plain result with `ListView.AddStr`. A separate, already-shipped mechanism -
`RowSegment`/`ColoredRow`/`ColoredRowRenderer` - exists for exactly this "draw text with a color"
need: it walks a row's segments, captures/restores the `ListView`'s current attribute (so a
selected row's own highlight still applies to the background), and is already used by both
`LiveLogDataSource` (multi-segment feed rows) and `ColoredRowListDataSource`
(`ShortcutPickerDialog`'s single-segment green key column).

## Goals / Non-Goals

**Goals:**
- Every `DrillableListView<T>` subclass's rows render their identifier text in
  `Theme.SubjectColor` (cyan).
- `SubscriptionsView`'s rows (subject patterns - pure identifiers, like a drillable list's rows)
  also render in `Theme.SubjectColor`.
- Reuse the existing `ColoredRowRenderer` draw path rather than hand-rolling a second
  viewport-slicing/padding implementation inside `PresenterListDataSource<T>`.
- Leave `HeaderEditorView`'s rows (key/value header pairs) unchanged - a header row isn't a single
  identifier, so it stays out of scope for this pass.

**Non-Goals:**
- Recoloring metadata (e.g. header key/value rows) green, or touching any other part of the UI
  (status bar, tab titles, dialogs beyond the existing Subject/Key coloring). Follow-on changes,
  not this one.
- Introducing a new theme color - this reuses `Theme.SubjectColor` as-is.
- Changing quick-search/filter match highlighting - out of scope for this pass.

## Decisions

**Add an optional color to `PresenterListDataSource<T>`, defaulted off, and delegate its draw to
`ColoredRowRenderer`.**

`PresenterListDataSource<T>` gains a constructor parameter `Color? textColor = null`. When set, an
item's formatted text is wrapped as a single-segment `ColoredRow` (`[new RowSegment(textColor,
text)]`) and drawn via `ColoredRowRenderer.Render` instead of the current hand-rolled
slice/pad/`AddStr` logic. `DrillableListView<T>`'s constructor passes `Theme.SubjectColor`
unconditionally (every drillable list is a pure identifier list).

**Give `ListEditorView<T>` the same optional `textColor` parameter, defaulted `null`, opted into
per subclass rather than applied uniformly like `DrillableListView<T>`.**

Unlike `DrillableListView<T>` (where every subclass is a pure identifier list), `ListEditorView<T>`
has one subclass that is (`SubscriptionsView`, subject patterns) and one that isn't
(`HeaderEditorView`, key/value pairs) - so the base class exposes the same constructor parameter
`ListEditorView<T>`'s `DrillableListView<T>` already has, but leaves the choice to each subclass
instead of always passing it. `SubscriptionsView` passes `Theme.SubjectColor`; `HeaderEditorView`
passes nothing (keeps the `null` default), so its rows keep drawing exactly as they do today.

Alternatives considered:
- **Separate `ColoredPresenterListDataSource<T>` type.** Rejected - it would duplicate
  `PresenterListDataSource<T>`'s identity/collection-change wiring for a one-line behavioral
  difference (a color passed through to render). A constructor parameter is the smaller diff and
  keeps one type.
- **Have each presenter (`StreamNamePresenter`, `KeyNamePresenter`, ...) return a `ColoredRow`
  instead of a `string`.** Rejected - `IValuePresenter<T>.Format` is also used by `ListEditorView<T>`
  (e.g. loading a row's text back into an edit field on Ctrl+E) and by any future non-colored
  consumer; overloading its return type couples formatting to a UI color concern it shouldn't need
  to know about. Coloring is purely a rendering choice of the data source, not the presenter.
- **Recolor in `DrillableListView<T>`'s own code around the shared `_dataSource`.** Not
  possible without the constructor parameter - the actual `AddStr` call happens inside
  `PresenterListDataSource<T>.Render`, so the color has to be known there.

## Risks / Trade-offs

- **Selected-row contrast is unverified for this specific text/background combination** →
  `ColoredRowRenderer` composes the segment's foreground with whatever background the `ListView`
  already set for that row (normal or selected), the same capture-and-restore idiom the live feed
  already relies on for `Theme.SubjectColor` - but the feed and a drillable list can have different
  selection-highlight schemes. Mitigation: visually check a highlighted row in each affected list
  (e.g. via `tmux capture-pane`, understanding colors won't render in that text dump - so this
  specifically needs a live terminal check, not just a capture-pane read) before calling this done.

## Migration Plan

Pure code change, no data/state migration. Ships in one commit; rollback is a plain revert.

## Open Questions

None.
