## Context

`Terminal.Gui.Views.ListView.SelectedItem` is `int?`. `SubscriptionsView` doesn't mutate `_items`
directly for add/delete (a NATS subscription can't be altered in place, so `Add`/`Replace`/`Delete`
redirect into `SubscriptionRegistry` instead); the local `_items` collection is instead kept in
sync by `RefreshFromRegistry()`, which does a full `_items.Clear()` then re-`Add()`s every
currently-active subscription, in response to the registry's own `Changed` event. The `Clear()`
step resets `SelectedItem` to `null`, and nothing re-selects a row afterward - even once the
re-`Add()` loop repopulates `_items` with one or more items.

This was masked until now by a second, since-fixed bug (`tab-content-focus-target`): Down-from-
header could land on a non-interactive container instead of the list itself, so "nothing appears
selected" had two independent causes tangled together. With that fixed, the list reliably gains
keyboard focus - exposing this one clearly: focus is correct, but there is genuinely no selected
item to highlight.

## Goals / Non-Goals

**Goals:**
- Whenever `ListEditorView<T>`'s item collection changes and ends up non-empty, guarantee its list
  has a valid `SelectedItem` afterward, without requiring each subclass to manage this itself.

**Non-Goals:**
- Preserving *which* item was selected across a full external rebuild (e.g. keeping the same
  subscription selected across an unrelated edit elsewhere in the list). `RefreshFromRegistry`'s
  Clear+rebuild discards that information before `ListEditorView<T>` ever sees it; recovering it
  would require changes to `SubscriptionsView`/`SubscriptionRegistry`, out of scope here. Defaulting
  to the first item is a strict improvement over no selection at all.
- Changing `RefreshFromRegistry`'s Clear+rebuild strategy itself.

## Decisions

**Fix in `ListEditorView<T>`'s existing `CollectionChanged` handler, not in `SubscriptionsView`.**
The same handler already added for the empty-state hint (`OnItemsChanged`) is extended to also
validate the list's selection. Considered fixing `RefreshFromRegistry` directly (e.g. remember and
restore the previously-selected subscription's identity across the rebuild): rejected as
higher-risk and higher-effort for this change, and it wouldn't protect any other `ListEditorView<T>`
consumer with the same external-rebuild shape.

**Default to index 0, not "nearest to the old index."** Since the triggering rebuild is a full
`Clear()` (a Reset notification) followed by fresh `Add()`s, there is no reliable "old index"
concept left in `_items` by the time `ListEditorView<T>` observes the change - the Clear event
itself already destroyed it. Index 0 is simple, deterministic, and correct for the common case
(the list had nothing selected and now has at least one item).

## Risks / Trade-offs

- [Defaulting to index 0 on every qualifying change could re-select item 0 even when the user's
  intent was elsewhere] → Only applies when there was no valid selection already; a valid existing
  selection is left untouched (guard checks `SelectedItem is null or out of range` before acting).
