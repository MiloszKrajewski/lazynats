## Why

Ctrl+N/Ctrl+D/Ctrl+R/Ctrl+E/Ctrl+F are currently bound directly on each list view (`DrillableListView<T>`, `ListEditorView<T>`, and the ad hoc Ctrl+F on `KeyListView`/`ObjectListView`), so they only work while that specific list has keyboard focus. Every management tab (`SubscribeTab`, `StreamsTab`, `ValuesTab`, `ObjectsTab`) hosts exactly one list as "the active one" at a time (drill-down tabs keep two lists alive but toggle which is `Visible`), so there is no reason the shortcuts should be this fussy about focus — moving them up to the tab and forwarding to whichever list is currently active makes them reachable regardless of which control inside the tab happens to hold focus.

## What Changes

- **BREAKING**: Ctrl+N/Ctrl+D/Ctrl+R/Ctrl+E/Ctrl+F are no longer bound as `KeyBindings` on the list views themselves. Each tab component determines its currently active/visible list and, for any key not otherwise handled, forwards it to that list's own advertised operations — invoking one only if the list actually supports it. The tab has no fixed idea of "the five keys"; it just asks whichever list is currently active what it supports.
- Each tab exposes exactly the shortcuts its *currently active* list supports (not a static union of everything the tab could ever show), so `IShortcutSource`/`ShortcutAggregator`/the Alt+K shortcut picker reflect reality: a check for "does the active list support this operation" happens before a shortcut is advertised, not just before it's invoked.
- `DrillableListView<T>` and `ListEditorView<T>` stop owning `KeyBindings` for these operations when hosted inside a tab; they instead expose which operations they support and an invocable action per operation (reusing/extending the existing `IShortcutSource`/`ShortcutHint` shape rather than inventing a parallel `CanX` surface).
- `ListEditorView<T>`'s standalone usage outside a tab (`HeaderEditorView` inside the modal `PublishDialog`) is unaffected — it keeps its own Ctrl+N/E/D bindings, since there is no owning tab to hoist them to.
- The two ad hoc Ctrl+F implementations (`KeyListView`'s server-side pre-fetch filter, `ObjectListView`'s client-side post-fetch filter) move to the same tab-owned dispatch, each still scoped to the tab/level where it already applies (never at the bucket-list level, never in `StreamsTab`/`SubscribeTab`).
- The `/` quick in-memory fuzzy search stays exactly as-is (list-focused, opt-in via `AttachFilterBox`) — it is not one of the five operations in scope here.

## Capabilities

### New Capabilities
- `tab-scoped-list-shortcuts`: the mechanism by which a tab owns Ctrl+N/D/R/E/F key bindings, resolves its currently active/visible child list, forwards the operation only if that list supports it, and advertises (via `IShortcutSource`) only the operations the active list currently supports.

### Modified Capabilities
- `drillable-list`: Create/Delete/Edit/Refresh (and, where present, Ctrl+F filter) are no longer bound directly on the list's own `KeyBindings` when the list is tab-hosted; the list instead exposes supported-operation/action pairs for the owning tab to bind and dispatch, and these operations move out of the list's own `IShortcutSource.Shortcuts` (Back/Search remain there).
- `list-editor`: Ctrl+N/Ctrl+E/Ctrl+D ownership moves to the owning tab for tab-hosted instances (`SubscribeTab`'s `SubscriptionsView`); standalone/modal usage (`HeaderEditorView`) is explicitly called out as retaining its own bindings.

`keyboard-shortcut-discovery` is unaffected: its "aggregation follows the focus chain, collecting from every ancestor that implements the shortcut-source contract" requirement already covers a tab advertising shortcuts while a descendant holds focus — only which views implement the contract changes, not the aggregation mechanism itself.

## Impact

- `src/lazynats/Components/DrillableListView.cs`, `src/lazynats/Components/ListEditorView.cs`, `src/lazynats/Components/IShortcutSource.cs`
- `src/lazynats/Subscriptions/SubscribeTab.cs`, `src/lazynats/Streams/StreamsTab.cs`, `src/lazynats/Values/ValuesTab.cs`, `src/lazynats/Objects/ObjectsTab.cs`
- `src/lazynats/Values/KeyListView.cs`, `src/lazynats/Objects/ObjectListView.cs` (ad hoc Ctrl+F bindings)
- Not affected: `src/lazynats/Publish/PublishDialog.cs`, `src/lazynats/Publish/HeaderEditorView.cs` (standalone modal usage keeps its own bindings), the `/` quick-search wiring, `ShortcutPickerDialog`/`ShortcutAggregator` internals (consumer behavior unchanged, only which views supply hints changes).
