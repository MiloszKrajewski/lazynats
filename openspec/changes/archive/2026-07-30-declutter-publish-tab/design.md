## Context

`PublishView` currently paints three full-width band backgrounds via `SetScheme` (Subject,
Headers, Payload) and renders its header list through a bespoke `IListDataSource`
(`HeaderListDataSource`) that also applies its own even/odd row coloring. The header list itself
is edited through two always-visible `TextField`s (`_headerKeyField`, `_headerValueField`) with
manual Ctrl+N/E/D wiring on `PublishView` (`ClearHeaderInput`, `LoadSelectedForEditing`,
`CommitHeaderInput`, `RemoveSelectedHeader`).

The app already has a shared "New/Edit/Delete a list of items via modal" component,
`ListEditorView<T>` (`src/lazynats/Components/ListEditorView.cs`), used today by
`SubscriptionsView` for subscription patterns via a single-field modal (`PatternDialog`) and a
row presenter (`SubscriptionPatternPresenter` implementing `IValuePresenter<T>`). Headers
(`HeaderPair(Key, Value)`, `src/lazynats/HeaderListDataSource.cs:10`) fit this same shape: a list
of items, each edited as one committed value.

## Goals / Non-Goals

**Goals:**
- Remove decorative coloring in `PublishView` (band backgrounds, header row zebra striping) that
  serves no functional purpose.
- Replace the bespoke inline header editor with the shared `ListEditorView<T>` pattern, matching
  how `SubscriptionsView` already edits its list.
- Keep the functional (non-decorative) Subject validity color as-is.

**Non-Goals:**
- Introducing any new "editable surface gets a background" convention (TextField/TextView/ListView
  shading) — deferred to a later, separate change.
- Changing NATS publish/send behavior, payload handling, or `SubscriptionRegistry`.
- Validating header text structure beyond non-empty (no colon-format enforcement).

## Decisions

**Delete `HeaderListDataSource`, don't de-color it.**
Once headers are edited via `ListEditorView<HeaderPair>`, rows render through the existing shared
`PresenterListDataSource<T>` (`src/lazynats/Components/PresenterListDataSource.cs`), which has no
per-row coloring. This removes the zebra striping as a byproduct of reuse rather than requiring a
separate edit to `HeaderListDataSource` — and then that file has no remaining purpose.
*Alternative considered*: keep `HeaderListDataSource` and just strip its `EvenRow`/`OddRow`
attributes. Rejected — it would leave a second, parallel list-rendering implementation for exactly
the shape `ListEditorView` already covers, which is the duplication this change is meant to
remove.

**Single-field `HeaderDialog`, formatted as `"Key: Value"`, split on first `:`.**
Mirrors `PatternDialog` exactly: one `TextField`, `Accepting` validates non-empty text and sets
`Result`, Esc cancels via `Dialog<TResult>`'s inherited behavior. `HeaderPresenter.Format` renders
`$"{pair.Key}: {pair.Value}"` for both list rows and the dialog's seed text on edit. On commit,
the raw text is split on the first `:`: everything before is `Key` (trimmed), everything after is
`Value` (trimmed). No colon present means the whole string becomes the key with an empty value —
no additional validation is added beyond the existing non-empty check, matching `PatternDialog`'s
level of strictness.
*Alternative considered*: keep two fields in the dialog (Key, Value), just move them into a modal
instead of an inline row. Rejected per explicit direction — the user wants the simpler
single-field "type it like `curl -H`" compromise, accepting that a missing colon is on the user.

**Headers own their `ObservableCollection<HeaderPair>` directly; `Add`/`Replace`/`Delete` stay
default.**
Unlike `SubscriptionsView`, which redirects `Add`/`Replace`/`Delete` through
`SubscriptionRegistry` because a live NATS subscription can't be mutated in place, headers have no
external system to synchronize with — `PublishView` only reads `_headers` at Send time. So
`HeaderEditorView` uses `ListEditorView<T>`'s default `Add`/`Replace`/`Delete` (direct collection
mutation), with no overrides needed.

**Header Ctrl+N/E/D scoped to the header editor's focus, not tab-wide.**
Today `PublishView` binds Ctrl+N/E/D on itself specifically so they work regardless of which of
Subject/Headers/Payload has focus. `ListEditorView` binds these on itself instead, so once headers
move to a nested `ListEditorView`, the shortcuts only fire when focus is inside that nested view.
Confirmed acceptable directly with the user — no extra bubbling/forwarding wired up to preserve
the old tab-wide behavior for headers.

## Risks / Trade-offs

- **[Risk]** The new header editor changes documented keyboard behavior (modal dialog instead of
  an always-visible input row), which is a `**BREAKING**` UX change for anyone used to the old
  flow. → Mitigation: matches the already-familiar `SubscriptionsView`/`PatternDialog` pattern
  elsewhere in the app, so the interaction model isn't new to the app as a whole, just to this
  tab.
- **[Risk]** Splitting on the first `:` with no validation means a header typed without a colon
  silently becomes `Key = <entire text>, Value = ""`, which may not be what the user intended. →
  Mitigation: explicitly accepted trade-off for simplicity; no mitigation implemented in this
  change.
- **[Risk]** Removing `PublishView`'s tab-wide Ctrl+N/E/D bindings means a user focused on
  Subject/Payload who presses Ctrl+N no longer jumps into header entry. → Mitigation: confirmed
  acceptable with the user; `ListEditorView`'s own binding still works once focus is in the header
  list.
