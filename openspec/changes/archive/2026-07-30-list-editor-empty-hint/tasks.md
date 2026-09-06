## 1. ListEditorView<T> empty-state hint

- [x] 1.1 Add a `protected virtual string EmptyHint` property to `ListEditorView<T>`, defaulted to
      a generic, non-empty message.
- [x] 1.2 Add a dim, non-focusable `Label` child to `ListEditorView<T>`, positioned/sized to match
      `_listView` (same `X`/`Y`/`Width`/`Height`), styled via a muted `Scheme`/`Attribute`
      following the existing pattern in `PublishView.cs`.
- [x] 1.3 Bind the hint label's text to `EmptyHint` and toggle its `Visible` based on
      `_items.Count == 0`, both at construction time and on `_items.CollectionChanged`.
- [x] 1.4 Confirm `SelectedIndex`, the Ctrl+N/E/D command handlers, and `PresenterListDataSource<T>`
      require no changes (per design.md, the hint is a non-interactive overlay only).

## 2. SubscriptionsView wiring

- [x] 2.1 Override `EmptyHint` on `SubscriptionsView` with subscription-specific wording (e.g.
      `"No subscriptions — Ctrl+N to add one"`).

## 3. Verification

- [x] 3.1 Run the app (`dotnet run --project src/lazynats`) and confirm: the Subscribe tab shows
      the hint on first launch with no subscriptions; adding a subscription hides the hint; deleting
      the last subscription shows it again; the hint is never reachable via list navigation and
      Ctrl+E/Ctrl+D remain no-ops while the list is empty.
- [x] 3.2 Confirm `PublishView`'s header list is unaffected (out of scope for this change).
