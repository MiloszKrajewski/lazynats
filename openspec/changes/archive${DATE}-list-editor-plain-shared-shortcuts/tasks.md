## 1. Drop the Ctrl modifier and fix Filter gating

- [x] 1.1 In `ListEditorView<T>`'s constructor, change `KeyBindings.Add(Key.N.WithCtrl, ...)`,
      `Key.E.WithCtrl`, `Key.D.WithCtrl` to plain `Key.N`/`Key.E`/`Key.D`.
- [x] 1.2 Move the `AddCommand(Command.FindNext, ...)` + `KeyBindings.Add(Key.F.WithCtrl, ...)`
      pair inside the `_bindSharedKeys && _filterEnabled` condition (currently only
      `_bindSharedKeys`), and change `Key.F.WithCtrl` to plain `Key.F`.
- [x] 1.3 Update the `Shortcuts` property's standalone-branch hints (`ListEditorView.cs:332-339`)
      to use `Key.N`/`Key.E`/`Key.D`/`Key.F` instead of their `.WithCtrl` forms.
- [x] 1.4 Update `ListEditorView.EmptyHint`'s default text from `"No items — Ctrl+N to add one"`
      to `"No items — N to add one"`.
- [x] 1.5 Update `HeaderEditorView.EmptyHint`'s override from `"No headers — Ctrl+N to add one"`
      to `"No headers — N to add one"`.

## 2. Verify

- [x] 2.1 Build the app (`dotnet build src/lazynats.sln`).
- [x] 2.2 Via `tmux`, open `PublishDialog` (Alt+P), focus the header list, and confirm: bare
      `N`/`E`/`D` create/edit/delete a header; `Ctrl+N`/`Ctrl+E`/`Ctrl+D` no longer do anything;
      neither `F` nor `Ctrl+F` opens a filter dialog; the empty-state hint reads
      "No headers — N to add one".
- [x] 2.3 Repeat the same check inside `TemplateDialog` (shares `HeaderEditorView`).
- [x] 2.4 Confirm `SubscriptionsView` (Subscribe tab) is unaffected — bare `N`/`E`/`D` still work
      via `SubscribeTab`'s existing dispatch, unchanged.
- [ ] 2.5 Open the shortcut picker (`?`) while the header list has focus and confirm it now lists
      plain `N`/`E`/`D` (no Filter entry), not `Ctrl+N`/`Ctrl+E`/`Ctrl+D`. **BLOCKED**: the `?`
      binding is registered only on `MainWindow`'s own `KeyDown` (deliberately not
      `BindKeyToApplication` — see `MainWindow.cs:160-167`), so it never fires while a modal
      `Dialog` (`PublishDialog`/`TemplateDialog`, the only places the header list's standalone
      mode is reachable) is running — confirmed via tmux: pressing `?` with the header list
      focused inside `PublishDialog` opens nothing. Pre-existing app behavior, unrelated to this
      change; there is currently no reachable standalone (`bindSharedKeys: true`) list editor
      that isn't hosted inside a modal `Dialog`, so this scenario can't be exercised as written.
