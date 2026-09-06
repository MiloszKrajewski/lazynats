## Why

`ListEditorView<T>` renders nothing when its item collection is empty, so a freshly opened
Subscribe tab is a flat black pane — indistinguishable from a broken or unloaded view. There's no
on-screen hint that Ctrl+N adds an item, which is especially costly on first launch, before a user
has learned the shortcut from anywhere else.

## What Changes

- `ListEditorView<T>` gains a dim, non-interactive hint line shown only while its item collection
  is empty, replacing the blank pane with an explicit "nothing here yet, here's how to fix that"
  message.
- A new `protected virtual string EmptyHint` on `ListEditorView<T>` lets each subclass supply its
  own wording; `SubscriptionsView` overrides it (e.g. `"No subscriptions — Ctrl+N to add one"`).
- The hint is a separate overlay view, not a fake row in the list data source: it's never
  selectable, never reachable by list navigation, and Ctrl+N/E/D behavior is unchanged.
- Scope is limited to `SubscriptionsView`, the only current `ListEditorView<T>` consumer.
  `PublishView`'s header list still hand-rolls its own list (`HeaderListDataSource`, per
  `doc/ui-design.md`) and is explicitly out of scope — deferred to a future change that migrates
  it onto `ListEditorView<T>`, at which point it gets this behavior for free.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `list-editor`: adds a requirement that `ListEditorView<T>` shows a non-interactive, per-subclass
  hint message in place of its list content when the item collection is empty.

## Impact

- `src/lazynats/Components/ListEditorView.cs` — new overlay child view, visibility toggled off
  `_items.CollectionChanged`, new `EmptyHint` extension point.
- `src/lazynats/Subscriptions/SubscriptionsView.cs` — overrides `EmptyHint`.
- No change to `PresenterListDataSource<T>`, `SelectedIndex`, or the Ctrl+N/E/D command wiring.
- No change to `PublishView` / `HeaderListDataSource` (out of scope, see above).
