## 1. `ListEditorView<T>` core

- [x] 1.1 Remove `_inputField` (`TextField`), `_editingIndex`, `ClearInput`, and the
      `UpdateInputValidity`/`InvalidInputAttribute` live-validation wiring from
      `src/lazynats/Components/ListEditorView.cs`.
- [x] 1.2 Remove the `Command.Up` binding and `Key.CursorUp` key binding that focuses the input at
      the top of the list (no longer meaningful with no input control present).
- [x] 1.3 Add `protected abstract bool TryCreate(out T result);` and
      `protected abstract bool TryEdit(T original, out T result);`.
- [x] 1.4 Rewire `Command.New` to call `TryCreate` and commit the result via a new overridable
      `Add(T value)` (default: append) on success; rewire `Command.Edit` to call `TryEdit` with the
      selected item's current value and commit via a new overridable `Replace(int, T value)`
      (default: replace in place) on success. Leave `Command.DeleteAll`/`Delete` as-is. (Split
      obtaining a value from committing it — see design.md's "Obtaining a value is separate from
      committing it" decision, discovered while implementing `SubscriptionsView`.)
- [x] 1.5 Update the `Shortcuts` (`IShortcutSource`) hints for New/Edit to invoke the new
      create/edit path instead of `ClearInput`/`Edit`.
- [x] 1.6 Remove the now-unused `OnParseError` hook (no free-text commit path left to fail parsing
      at this layer).

## 2. `IValuePresenter<T>` and row rendering

- [x] 2.1 Drop `TryParse` from `src/lazynats/Components/IValuePresenter.cs`, leaving only
      `Format`.
- [x] 2.2 Confirm `src/lazynats/Components/PresenterListDataSource.cs` still compiles unchanged
      (it only calls `Format`).

## 3. `SubscriptionsView` modal

- [x] 3.1 Add `PatternDialog: Dialog<string>` (single `TextField` seeded from `original.Pattern`
      for edit / empty for create, Cancel/OK buttons in that order) in
      `src/lazynats/Subscriptions/PatternDialog.cs`.
- [x] 3.2 Implement `TryCreate`/`TryEdit` on `SubscriptionsView` using that dialog, moving the
      pattern validation currently in `SubscriptionPatternPresenter.TryParse` into the dialog's own
      OK-gating logic.
- [x] 3.3 Override `Add`/`Replace` (not `TryCreate`/`TryEdit`) on `SubscriptionsView` to redirect
      into `SubscriptionRegistry.Add`/`Remove` instead of touching `_items` directly (`_items` stays
      a mirror synced by `RefreshFromRegistry`); `Replace` removes the old subscription then adds
      the edited pattern under a new identity, matching prior behavior. Keep the existing `Delete`
      override.
- [x] 3.4 Shrink `src/lazynats/Subscriptions/SubscriptionPatternPresenter.cs` to `Format` only.

## 4. Verification

- [x] 4.1 Manually run the app against a local NATS server: Ctrl+N adds a subscription via the
      modal, Ctrl+E edits one (new identity, old one stopped), Ctrl+D deletes one, Cancel (Escape)
      on the edit modal leaves the list unchanged. Verified via a tmux-driven session against the
      already-running dockerized `nats-server` on `localhost:4222`. Found and fixed a
      `NullReferenceException` in `PatternDialog` (see design.md) in the process.
- [x] 4.2 Manually verify arrow-key navigation in the Subscribe tab (Up/Down within the list,
      Alt+B/Alt+P tab switching) has no leftover focus-coordination glitches now that the input is
      gone. Confirmed: repeated Up at the top of a 2-item list neither crashes nor switches tabs,
      and Alt+P/Alt+B switch tabs cleanly with the list contents intact.
- [x] 4.3 `dotnet build src/lazynats.sln` clean.

## 5. Finalize

- [x] 5.1 Checked `doc/UI.md` for references to the old inline-input list-editor shape — the only
      "input row" mention there is `PublishView`'s header editor (explicitly out of scope, see
      design.md's Non-Goals), so no change needed.
- [x] 5.2 Run `openspec validate simplify-list-editor-modal` before archiving.
