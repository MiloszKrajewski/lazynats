## Context

`ListEditorView<T>` (`src/lazynats/Components/ListEditorView.cs`) hosts a single
`Terminal.Gui.Views.ListView` bound to `PresenterListDataSource<T>`. `Terminal.Gui.Views.ListView`
only paints its focus/selection highlight on rows below `source.Count`; when the collection is
empty, `Render` is never invoked for any row and the pane draws as a flat, uncolored area. In
practice this reads as a blank/broken pane rather than an intentionally empty list, especially on
first launch of the Subscribe tab before any subscription has been added.

(Separately, the codebase had observed that after adding then deleting the sole item, the
`ListView`'s internal `SelectedItem` cursor leaves a highlighted row behind. That turned out to be
an artifact of `Terminal.Gui.Views.ListView` not resetting its selection state when the backing
collection empties out — not a deliberate empty-state UI — so this design does not try to
reproduce it. It builds a real empty-state affordance instead.)

## Goals / Non-Goals

**Goals:**
- Replace the blank pane with a clear, dim hint line when `ListEditorView<T>`'s item collection is
  empty, telling the user how to add an item.
- Let each subclass supply its own hint wording without duplicating the empty/non-empty toggling
  logic.
- Keep the hint purely visual: it must not be selectable, editable, or deletable, and must not
  change `SelectedIndex`, `PresenterListDataSource<T>`, or the Ctrl+N/E/D command wiring.

**Non-Goals:**
- Migrating `PublishView`'s hand-rolled header list (`HeaderListDataSource`) onto
  `ListEditorView<T>`. It keeps its own empty-state behavior (or lack thereof) until a future
  change performs that migration; at that point it inherits this hint for free.
- Reproducing the old "highlighted empty row" look from the add-then-delete sequence — that was an
  unintended `Terminal.Gui.Views.ListView` artifact, not a target behavior.
- Any change to how items are counted, formatted, or rendered while the collection is non-empty.

## Decisions

**Overlay view, not a fake list row.** The hint is a second child view (a `Label`) added to
`ListEditorView<T>` alongside `_listView`, occupying the same `X/Y/Width/Height`, with its
`Visible` toggled by `_items.CollectionChanged` (and set once in the constructor for the initial
state). Considered instead making `PresenterListDataSource<T>.Count` report `1` and rendering hint
text for a synthetic row at index 0: rejected because it would make the hint reachable through
list navigation and would require every call site that reasons about `_items.Count` (e.g.
`SelectedIndex`) to special-case the synthetic row. An overlay view keeps the hint entirely outside
the list's selection model — `SelectedIndex` already returns `null` whenever `_items.Count == 0`,
so Ctrl+N/E/D behave correctly with no additional guards.

**Per-subclass wording via a virtual property.** `ListEditorView<T>` exposes
`protected virtual string EmptyHint`, defaulted to a generic message, which `SubscriptionsView`
overrides. Considered a constructor parameter instead: rejected because `ListEditorView<T>`'s
constructor already takes `items` and `presenter`, and a virtual property reads better at the call
site than a third positional string, and mirrors how `TryCreate`/`TryEdit` are already overridden
rather than injected.

**Dim, non-interactive styling.** The hint `Label` gets a muted `Scheme`/`Attribute`, following the
existing pattern of per-view `Scheme(new Attribute(...))` overrides already used in
`PublishView.cs` for its field bands, rather than introducing a new styling mechanism.

## Risks / Trade-offs

- [Overlay view drawn on top of an empty `ListView`] → Low risk: `ListView` paints nothing when
  `Count == 0`, so there's no visible content for the overlay to conflict with; the overlay is only
  `Visible` in exactly that state.
- [Hint text becomes stale if `EmptyHint` isn't overridden for a future `ListEditorView<T>`
  subclass] → Mitigated by defaulting `EmptyHint` to a generic-but-still-useful message rather than
  leaving it abstract, so a forgotten override degrades gracefully instead of breaking.
