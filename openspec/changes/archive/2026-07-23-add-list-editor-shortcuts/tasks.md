## 1. Presenter contract and list editor scaffolding

- [x] 1.1 Add `IValuePresenter<T>` (`Format(T)`, `TryParse(string, out T, out string?)`) as its own
      file under `src/lazynats/`.
- [x] 1.2 Add `ListEditorView<T>` scaffolding: constructor taking an `ObservableCollection<T>` and an
      `IValuePresenter<T>`, top `TextField`, `ListView` bound to the collection, layout matching the
      existing single-column-list style already used by `SubscriptionsView`/`HeaderListDataSource`
      (no parallel bookkeeping list).

## 2. Core editing mechanics

- [x] 2.1 Implement `protected virtual void Append(string raw)`: on successful parse, add to the
      collection (or replace at the tracked edit index if one is set) and clear the input; on failed
      parse, call `OnParseError` and leave state unchanged.
- [x] 2.2 Implement `protected virtual void Edit(int index)`: format the selected item into the input,
      track the index, focus the input.
- [x] 2.3 Implement `protected virtual void Delete(int index)`: remove the item from the collection.
- [x] 2.4 Implement `protected virtual void ClearInput()`: clear the input text and drop any tracked
      edit index, then focus the input.
- [x] 2.5 Register `Ctrl+D`/`Ctrl+E`/`Ctrl+N` via `AddCommand`/`KeyBindings.Add` on the
      `ListEditorView<T>` itself (not per-child-control), following the existing rationale documented
      in `PublishView.cs`.
- [x] 2.6 Wire `Enter` in the text input to `Append`.

## 3. Validation feedback

- [x] 3.1 Add live per-keystroke validation on the text input (`ValueChanged`): call
      `presenter.TryParse`, discard the out-values, and toggle the input's scheme between normal and
      invalid, reusing `PublishView`'s existing `InvalidSubject`-style `Attribute` convention.
- [x] 3.2 Add `protected virtual void OnParseError(string raw, string? error)` (default no-op),
      invoked from `Append` on a failed commit-time parse.

## 4. Shortcut discovery mechanism

- [x] 4.1 Add `ShortcutHint` (record: `Key`, `Text`, `Action`) and `IShortcutSource`
      (`IEnumerable<ShortcutHint> Shortcuts { get; }`) as their own file under `src/lazynats/`.
- [x] 4.2 Add the focus-chain aggregator: given a starting `View` (the currently focused view), walk
      `SuperView` up to the root, collecting `Shortcuts` from every ancestor (including the start view)
      that implements `IShortcutSource`.
- [x] 4.3 Add a helper that subscribes to `Application.Navigation.FocusedChanged` and re-runs the
      aggregator on each change, exposing the current combined shortcut set (e.g. via an event or
      observable property) for a future consumer to render — without wiring it into `MainWindow`'s
      `StatusBar` in this change. (Implemented against the owning `IApplication` instance rather
      than the static `Application.Navigation`, which is obsolete in 2.4.10.)

## 5. Tie the two pieces together

- [x] 5.1 Implement `IShortcutSource` on `ListEditorView<T>`, advertising its own Ctrl+D/E/N bindings
      (with a way for a derived type to add to or override the advertised set, consistent with the
      other `protected virtual` extension points).
- [x] 5.2 Add a `"key: value"` header presenter (`IValuePresenter<HeaderPair>`-shaped, reusing the
      existing `HeaderPair` type) as the concrete example presenter proving the abstraction — not wired
      into `PublishView`.

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` succeeds with the new files included.
- [x] 6.2 Manually exercise `ListEditorView<T>` and the shortcut aggregator against a throwaway
      presenter from a minimal harness (temporarily hooked into `Program.cs` behind a
      `--verify-list-editor` flag so it never reached the NATS connection path, then fully removed)
      to confirm: append, edit-in-place, delete, invalid-input rejection with `OnParseError` carrying
      the presenter's message, and that focus-chain aggregation collects an editor's own shortcuts
      plus an ancestor `IShortcutSource`'s while skipping unrelated views. 19/19 checks passed.
      **Not independently exercised**: the live per-keystroke red/normal input coloring and the
      actual `Ctrl+D/E/N` key-press-through-`KeyBindings` path, since both require a running
      Terminal.Gui input loop rather than direct method calls — `AddCommand`/`KeyBindings.Add` wiring
      matches the already-proven `PublishView` pattern exactly, and `SetFocus()`/scheme-setting calls
      were confirmed not to throw when invoked outside a running `Application`, but true interactive
      verification is deferred to the follow-up change that actually wires this into a visible tab.
- [x] 6.3 Confirm no changes were made to `SubscriptionsView.cs`, `PublishView.cs`,
      `HeaderListDataSource.cs`, `LiveUpdatesView.cs`, or `MainWindow.cs`, per the change's scope.
      Confirmed via `git status`/`git diff`: none of the five files differ from their state at the
      start of this change; `Program.cs` was temporarily edited for the harness hook and fully
      reverted (`git diff` shows no changes) before this task was marked done.
