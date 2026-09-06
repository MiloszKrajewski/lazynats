## Why

Two management-tab widgets (`SubscriptionsView`'s pattern list, `PublishView`'s header list) have each
grown their own ad hoc "text input + editable list" wiring, with `PublishView`'s header editor already
duplicating manual edit-index tracking and a custom `IListDataSource` that `SubscriptionsView` doesn't
have. As more management tabs (streams, consumers, KV/OBJ stores) are added, this pattern will keep
recurring. At the same time, the app has no way for a user to discover what keyboard shortcuts are
available on the currently focused widget short of trial-and-error, which conflicts with the goal of a
fully keyboard-driven TUI where buttons are not the only way to act.

## What Changes

- Add a generic, reusable `ListEditorView<T>` component: single-line text input on top, a
  `ListView` below bound to an injected `ObservableCollection<T>`. Text↔`T` conversion is supplied by an
  injected presenter (`Format`/`TryParse`), not hardcoded per consumer.
- `Enter` appends a parsed value (or commits an in-place edit); `Ctrl+E` loads the selected item back
  into the input for editing; `Ctrl+D` deletes the selected item; `Ctrl+N` clears the input and cancels
  any in-progress edit. All key bindings live on the component itself so they fire regardless of which
  child control has focus (matching the existing pattern already used in `PublishView`).
- Live per-keystroke validation recolors the input field (reusing `PublishView`'s existing invalid-field
  styling convention); a failed commit attempt on `Enter` additionally invokes an overridable
  `OnParseError` hook carrying the presenter's error message, without blocking further edits.
- Core operations (`Append`, `Edit`, `Delete`, `ClearInput`) are `protected virtual`, so a subclass can
  override behavior beyond the text↔`T` mapping (e.g. changing what "Enter" does).
- Add an app-wide, opt-in keyboard-shortcut discoverability mechanism: an `IShortcutSource` interface a
  `View` implements to declare which of its own key bindings should be shown to the user, plus an
  aggregator that walks the currently-focused view up to the root (the same ancestor chain Terminal.Gui
  already uses to bubble key events) and renders the collected shortcuts into the status bar, replacing
  the need to hand-wire a `Shortcut` + focus-visibility toggle per widget as `MainWindow` does today for
  `clearShortcut`/`publishStatusShortcut`.
- `ListEditorView<T>` implements `IShortcutSource` for its own Ctrl+D/E/N bindings, serving as the first
  real consumer proving the mechanism end-to-end.
- Out of scope for this change: migrating `SubscriptionsView` or `PublishView` to use `ListEditorView<T>`,
  and retrofitting `IShortcutSource` onto any existing view (`SubscriptionsView`, `PublishView`,
  `LiveUpdatesView`). Both are deferred to a follow-up change once this infrastructure lands.

## Capabilities

### New Capabilities
- `list-editor`: reusable text-input-plus-editable-list component (`ListEditorView<T>`) — append/edit/
  delete/clear behavior, presenter-driven parsing/formatting, validation feedback.
- `keyboard-shortcut-discovery`: opt-in mechanism (`IShortcutSource` + focus-chain aggregation) for
  surfacing a focused widget's key bindings in the status bar.

### Modified Capabilities
- None. `nats-publish` and `nats-subscriptions` behavior is unchanged; those views are not touched by
  this change.

## Impact

- New files under `src/lazynats/`: the `ListEditorView<T>` component, its presenter interface, and the
  `IShortcutSource` interface plus its aggregator.
- No changes to `SubscriptionsView.cs`, `PublishView.cs`, `HeaderListDataSource.cs`, `LiveUpdatesView.cs`,
  or `MainWindow.cs` in this change — these components are built standalone and proven via the
  `ListEditorView<T>` self-consumption of `IShortcutSource`, with no existing tab wired to either piece
  yet.
