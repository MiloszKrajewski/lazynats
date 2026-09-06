## 1. Base class accessor

- [x] 1.1 Add `protected int? EditingIndex => _editingIndex;` to `ListEditorView<T>`
      (`src/lazynats/ListEditorView.cs`). No other change to that file.

## 2. Presenter

- [x] 2.1 Add `SubscriptionPatternPresenter : IValuePresenter<SubscriptionInfo>` (own file under
      `src/lazynats/`): `Format` returns `value.Pattern`; `TryParse` trims the input, rejects
      empty/whitespace-only text, and otherwise returns `new SubscriptionInfo(Guid.Empty, pattern)`.

## 3. Rewrite `SubscriptionsView`

- [x] 3.1 Change `SubscriptionsView` to `internal sealed class SubscriptionsView:
      ListEditorView<SubscriptionInfo>`, with the public constructor chaining to a private one that
      passes `new ObservableCollection<SubscriptionInfo>(registry.Active)` to the base constructor
      and keeps its own field referencing that same collection instance, plus a
      `private static readonly SubscriptionPatternPresenter Presenter = new();`.
- [x] 3.2 Override `Append(string raw)`: parse via `Presenter.TryParse` (call `OnParseError` and
      return on failure); on success, if `EditingIndex` points at a valid item, `_registry.Remove` its
      `Id` first; then `_registry.Add(value.Pattern)`; then `ClearInput()`.
- [x] 3.3 Override `Delete(int index)`: `_registry.Remove(_items[index].Id)`; if `index ==
      EditingIndex`, also call `ClearInput()`.
- [x] 3.4 Do NOT override `Edit` — confirm the inherited default (format via presenter, track index,
      focus input) is used as-is.
- [x] 3.5 Replace `RefreshFromRegistry` to clear and repopulate the kept `_items` collection from
      `_registry.Active`; subscribe in the constructor, unsubscribe in an overridden `Dispose(bool)`.
- [x] 3.6 Remove the `_patternField`/`addButton`/manual `AddCommand(Command.DeleteAll)`/`Key.Delete`
      wiring entirely — all superseded by the base class's own input field, `ListView`, and Ctrl+N/E/D
      bindings. No "_Add" button in the rewritten view.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 4.2 A real `nats-server` is already running locally (`docker ps` showed a `nats:latest`
      container publishing `4222`), so verification uses the actual `SubscriptionRegistry` against a
      real `NatsConnection` rather than a double — no coverage gap to document here.
- [x] 4.3 Via a temporary harness (hooked behind a `--verify-subscriptions-list-editor` flag in
      `Program.cs`, fully removed afterward): confirmed adding a pattern calls `Registry.Add` and the
      view's mirrored list reflects it; Ctrl+E-style edit-and-commit removes the old subscription and
      adds a new one (different `Id`, edited pattern text); Ctrl+D-style delete removes the selected
      subscription; invalid (empty) input is rejected without touching the registry. 8/8 checks
      passed against the live server.
- [x] 4.4 Confirmed via `git status`/`git diff`: `PublishView.cs`, `HeaderListDataSource.cs`,
      `LiveUpdatesView.cs`, `MainWindow.cs`, and `SubscriptionRegistry.cs` are unchanged from their
      state at the start of this change.
