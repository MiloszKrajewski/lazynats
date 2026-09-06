## 1. Base plumbing: `ITabOperationsSource` and the list-side split

- [x] 1.1 Add `internal interface ITabOperationsSource { IEnumerable<ShortcutHint> TabOperations { get; } }` alongside `IShortcutSource` in `Components/IShortcutSource.cs`.
- [x] 1.2 `DrillableListView<T>`: remove the base constructor's `KeyBindings.Add(Key.R.WithCtrl, Command.Refresh)` call (keep the `AddCommand` registration). Remove the `KeyBindings.Add` calls in `EnableCreate`/`EnableDelete`/`EnableEdit` (keep the enabled-flag sets, `AddCommand` registrations, and — in `EnableCreate` — the inner `ListView`'s native Ctrl+N alias removal).
- [x] 1.3 `DrillableListView<T>`: implement `ITabOperationsSource.TabOperations`, returning the Refresh/New/Delete/Edit hints (same shape as today's `Shortcuts`, gated by the same enabled flags) — this is what today's `Shortcuts` computed for those four.
- [x] 1.4 `DrillableListView<T>`: trim `Shortcuts` down to just Back (`Esc`, if ascend enabled) and Search (`/`, if a `FilterBox` is attached).
- [x] 1.5 `KeyListView`: move the Ctrl+F "Filter" append from its `Shortcuts` override to a `TabOperations` override (`base.TabOperations.Append(...)`); remove its own `KeyBindings.Add(Key.F.WithCtrl, Command.Open)` (keep the `AddCommand` registration).
- [x] 1.6 `ObjectListView`: same as 1.5 for its Ctrl+F "Filter" append — but its `Shortcuts` override must keep the Ctrl+S "Download" append (out of scope, stays list-bound) while only the Ctrl+F append moves to `TabOperations`. Remove its own `KeyBindings.Add(Key.F.WithCtrl, Command.Open)` (keep `AddCommand`); leave the Ctrl+S binding untouched.
- [x] 1.7 `ListEditorView<T>`: add a `bool bindSharedKeys = true` constructor parameter gating the three `KeyBindings.Add` calls (Ctrl+N/E/D). Keep the `AddCommand` registrations unconditional.
- [x] 1.8 `ListEditorView<T>`: implement `ITabOperationsSource.TabOperations` returning today's New/Edit/Delete hints. Make `Shortcuts` conditional on `bindSharedKeys`: unchanged (New/Edit/Delete) when `true`, empty when `false`.
- [x] 1.9 `SubscriptionsView`: pass `bindSharedKeys: false` to the base constructor. Confirm `HeaderEditorView` passes nothing (keeps default `true`) — no change needed there.

## 2. `StreamsTab`

- [x] 2.1 Add a `private ITabOperationsSource _shortcutSource` field plus a `SetShortcutSource(ITabOperationsSource source)` setter; call it once in the constructor (initial/top level) and again in each of `Descend`/`Ascend`, right alongside the other per-level state transitions (per design.md Decision 3 — superseded from an earlier computed-ternary draft during implementation review).
- [x] 2.2 Override `View.OnKeyDownNotHandled(Key key)` on `StreamsTab` itself: look up `_shortcutSource.TabOperations` for a hint matching `key` and invoke its `Action` if found, falling through to `base.OnKeyDownNotHandled(key)` otherwise (per design.md Decision 1 — superseded from an earlier fixed `KeyBindings.Add`/`Dispatch(Key)` helper draft during implementation review, which baked in a hardcoded key set).
- [x] 2.3 Implement `IShortcutSource.Shortcuts => _shortcutSource.TabOperations;` on `StreamsTab`.
- [x] 2.4 Verify (read-through, no behavior change expected) that `_listView.RefreshRequested`/`CreateRequested`/`DeleteRequested`/`EditRequested` and `_consumerListView`'s equivalents are still wired exactly as today (`StreamsTab.cs:56-61,70-75`) — `OnKeyDownNotHandled` invokes the same events, just via `TabOperations`'s `Action` instead of a direct `KeyBindings` hit.

## 3. `ValuesTab`

- [x] 3.1 Same as 2.1–2.3 (`_shortcutSource`/`SetShortcutSource`/`OnKeyDownNotHandled`), toggled alongside `ValuesTab`'s existing bucket/key `Descend`/`Ascend` transitions, covering Ctrl+F in addition to Ctrl+R/N/D/E (confirm which levels support Edit — mirror existing `Enable*()` calls, don't add new affordances).
- [x] 3.2 Confirm `KeyListView.FilterRequested` → `ValuesTab.OpenKeyFilterDialog` wiring is unaffected — `OnKeyDownNotHandled` calls the same handler via `TabOperations`.

## 4. `ObjectsTab`

- [x] 4.1 Same as 2.1–2.3 (`_shortcutSource`/`SetShortcutSource`/`OnKeyDownNotHandled`), toggled alongside `ObjectsTab`'s existing bucket/object `Descend`/`Ascend` transitions, covering Ctrl+F. Note `ObjectListView` has no Edit wiring — don't bind/advertise Ctrl+E for the object level.
- [x] 4.2 Confirm `ObjectListView.FilterRequested`/`DownloadRequested` wiring is unaffected — `FilterRequested` now reaches `ObjectsTab` via `OnKeyDownNotHandled`/`TabOperations`; `DownloadRequested` (Ctrl+S) is untouched, still bound directly on `ObjectListView`.

## 5. `SubscribeTab`

- [x] 5.1 Override `View.OnKeyDownNotHandled(Key key)` on `SubscribeTab` itself, reading `_subscriptionsView.TabOperations` directly (single list, no branching, so no `_shortcutSource`/`SetShortcutSource` needed) via the same lookup-and-invoke pattern as `StreamsTab` (no Ctrl+R/Ctrl+F — `ListEditorView<T>` has neither).
- [x] 5.2 Implement `IShortcutSource.Shortcuts => _subscriptionsView.TabOperations;` on `SubscribeTab`.

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` — confirm it compiles clean.
- [x] 6.2 Via `tmux` (per `CLAUDE.md`), against a running `nats-server`: for `StreamsTab`, move focus to the details pane (Tab past the list) and confirm Ctrl+N/D/E/R still work; repeat after descending into a stream's consumer list.
- [x] 6.3 Via `tmux`, for `ValuesTab`/`ObjectsTab`: confirm Ctrl+F opens the filter dialog with focus on the details pane at the key/object level, and confirm Ctrl+F does nothing (no dialog, no error) at the bucket level.
- [x] 6.4 Via `tmux`, confirm Alt+K's shortcut picker shows exactly the active list's supported operations — descend/ascend within `StreamsTab`/`ValuesTab`/`ObjectsTab` and reopen the picker each time to confirm the advertised set changes (e.g. Edit hint disappears at a level that doesn't support it).
- [x] 6.5 Via `tmux`, confirm `/` quick-search and Esc/Backspace ascend still only work while the list (or its search field) itself holds focus — pressing them with focus on the details pane should have no effect, confirming they were correctly left out of the tab-level dispatch.
- [x] 6.6 Via `tmux`, open `PublishDialog` (Alt+P) and confirm `HeaderEditorView`'s Ctrl+N/E/D still work exactly as before (standalone `ListEditorView<T>` path, `bindSharedKeys: true`).
