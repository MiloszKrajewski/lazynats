## Why

After adding the first subscription to an empty Subscribe tab, the new row shows but is not
selected: it doesn't highlight, and Ctrl+E/Ctrl+D are silent no-ops until the user manually presses
Down to establish a selection. The root cause is `Terminal.Gui.Views.ListView.SelectedItem`
(`int?`) becoming `null` whenever `SubscriptionsView.RefreshFromRegistry()` runs - which does a
full `_items.Clear()` followed by re-`Add()`-ing every active subscription, on *every* registry
change (add, delete, or edit), not just the one that changed. `ListEditorView<T>` never
re-establishes a selection afterward, so the list is left with no selected item even though it now
has content.

This surfaced as a side effect of testing `tab-content-focus-target` (which fixed a related but
distinct bug: Down-from-header landing on the wrong view). Once that was fixed, the list correctly
gains keyboard focus, but with no valid selection - which is what actually produced the "nothing
highlighted" symptom being chased.

## What Changes

- `ListEditorView<T>` ensures its list has a valid selection whenever its item collection changes
  and a selection doesn't already exist: if `_items.Count > 0` and the underlying `ListView`'s
  `SelectedItem` is `null` or out of range, it's set to `0`.
- This lives in the shared base class, so it fixes `SubscriptionsView` (whose add/delete goes
  through an async registry round-trip and a full collection rebuild) without any
  `SubscriptionsView`-specific code, and protects any future `ListEditorView<T>` consumer with a
  similar "external source of truth, rebuilt wholesale on change" pattern.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `list-editor`: adds a requirement that the list editor maintains a valid selection whenever its
  item collection is non-empty, re-establishing one (defaulting to the first item) if the
  underlying list view's selection becomes invalid as a side effect of how the collection changed.

## Impact

- `src/lazynats/Components/ListEditorView.cs` - extend the existing `_items.CollectionChanged`
  handler (already added for the empty-state hint) to also validate/restore selection.
- No changes to `SubscriptionsView`, `PublishView`, or `ManagementTabs`.
