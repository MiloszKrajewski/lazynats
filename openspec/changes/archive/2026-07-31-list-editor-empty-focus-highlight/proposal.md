## Why

When a `ListEditorView<T>` is empty, the list content is fully covered by a dim, non-interactive
hint label (`_emptyHintLabel` in `ListEditorView.cs`). That label always renders with the
`Disabled` scheme, whether or not the list editor currently holds keyboard focus. With no rows to
show a focus highlight, an empty list editor looks identical whether it's focused or not — the
user has no visual cue that Ctrl+N/E/D (or any other input) is currently scoped to that control.

## What Changes

- While a `ListEditorView<T>` is empty, its hint label SHALL render with a focused/highlighted
  style when the list editor holds keyboard focus, and its existing dim style otherwise.
- The hint remains non-interactive and outside the list's selection model — only its visual style
  changes with focus, not its behavior (still can't be navigated onto or selected).

## Capabilities

### Modified Capabilities
- `list-editor`: the empty-state hint's requirement is extended so its visual style reflects
  whether the list editor currently holds keyboard focus, in addition to the existing "dim,
  non-interactive" baseline appearance.

## Impact

- `src/lazynats/Components/ListEditorView.cs`: `_emptyHintLabel` needs to react to the list
  editor's focus state (gained/lost) and swap its `Scheme` accordingly, mirroring how a focused
  list row is normally highlighted.
- No change to `SubscriptionsView` or `PublishTab`'s header editor call sites — both inherit the
  behavior automatically as `ListEditorView<T>` subclasses.
