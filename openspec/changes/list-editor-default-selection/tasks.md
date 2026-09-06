## 1. Selection recovery

- [x] 1.1 Extend `ListEditorView<T>`'s `_items.CollectionChanged` handler to also ensure a valid
      selection: if `_items.Count > 0` and `_listView.SelectedItem` is `null` or out of range, set
      it to `0`.
- [x] 1.2 Call the same check once at construction time, alongside the existing initial
      `UpdateEmptyHintVisibility()` call, so a `ListEditorView<T>` constructed with pre-existing
      items also starts with a valid selection.

## 2. Verification

- [x] 2.1 Run the app and reproduce the originally reported sequence: Down, Up, Ctrl+N, type a
      pattern, Enter, Up, Down. Confirm the row is highlighted immediately (no extra Down needed),
      and Ctrl+D deletes it.
- [x] 2.2 Confirm deleting a subscription (leaving others in the list) leaves a valid selection
      behind rather than losing it.
- [x] 2.3 Confirm `PublishView`'s header list (which doesn't go through `ListEditorView<T>`) is
      unaffected.
