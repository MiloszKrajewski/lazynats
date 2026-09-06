## Why

MainWindow and `MessageDetailDialog` each hand-roll their own `?`-triggers-the-shortcut-picker
KeyDown handler: same re-entrancy-avoiding `AddTimeout(Zero, ...)` deferral, same
`ShortcutAggregator.Collect(...)` → `new ShortcutPickerDialog(hints)` → `app.Run(...)` →
`picker.Result?.Action()` sequence, explained by nearly-duplicate comment blocks at each site.
The only genuine difference between the two is which view `Collect` starts walking from. Any
future dialog that implements `IShortcutSource` and wants `?` to work while it's open (dialogs are
their own top-level with no `SuperView` link back to MainWindow, so MainWindow's binding never
sees keys pressed inside one) would otherwise copy this block a third time — `PublishDialog`,
whose `HeaderEditorView` already implements `IShortcutSource`, is exactly such a dialog, just one
that had never had `?` wired up at all.

## What Changes

- Extract the shared "open the shortcut picker for a given start view" logic into a small helper
  in `Components/` (`ShortcutPickerLauncher`), parameterized by a `Func<View?> startView` supplier
  so a caller can still choose its own starting point for `ShortcutAggregator.Collect(...)` when
  needed (MainWindow needs `App.TopRunnableView?.MostFocused`, since it has no single "owner" view
  to walk from the way a dialog does).
- Provide two entry points on the helper: one that returns the reusable `Action` (for MainWindow,
  which folds `?` into its existing `ShortcutHint`-list dispatch and status-bar widget), and one
  that binds the `?` `KeyDown` handler directly on a given owner view (for dialogs, which have no
  such hint-list dispatch of their own). The direct-binding form's `startView` is optional: it
  defaults to a runtime heuristic (`ResolveStartView`) that auto-detects, per call, whether the
  view's current focused descendant reaches back to the owner or not — see design.md for why this
  removes the need to hand-reason a fixed policy per call site, while still allowing an override
  for a caller that needs one.
- Update `MainWindow` and `MessageDetailDialog` to call the helper instead of each maintaining
  their own copy of the dance.
- Wire `PublishDialog` up to the same helper (`ShortcutPickerLauncher.BindKey(this)`, no override
  needed): a genuine new capability, not just a refactor — `?` previously did nothing in
  `PublishDialog` and now opens the picker there too, listing its `HeaderEditorView`'s shortcuts.
  Made trivial to add specifically because the extraction's auto-resolving default removed the
  per-caller reasoning a third call site would otherwise have required.
- `?` continues to work exactly as it did before in `MainWindow` and `MessageDetailDialog` — the
  only observable behavior change is `PublishDialog` gaining `?` support.

## Capabilities

### New Capabilities

- `?` (Shortcuts) now opens the shortcut picker from within `PublishDialog`, listing its
  `HeaderEditorView`'s shortcuts — previously `?` had no binding there at all.

### Modified Capabilities

(none — `shortcut-picker`'s existing requirements for `MainWindow` and `MessageDetailDialog` are
unaffected; those two call sites change implementation structure only, not observable behavior)

## Impact

- `src/lazynats/Components/ShortcutPickerLauncher.cs`: new helper file alongside
  `ShortcutAggregator.cs`/`IShortcutSource.cs`/`ShortcutPickerDialog.cs`.
- `src/lazynats/MainWindow.cs`: `?` entry in `topLevelShortcuts` now builds its `Action` via the
  helper instead of inline.
- `src/lazynats/LiveFeed/MessageDetailDialog.cs`: its hand-rolled `?` `KeyDown` subscriber and
  accompanying comment block are replaced with a single call to the helper.
- `src/lazynats/Publish/PublishDialog.cs`: gains a single `ShortcutPickerLauncher.BindKey(this)`
  call, giving it working `?` support for the first time.
- `src/lazynats/Program.cs`: unrelated `#if DEBUG`-guarded dev convenience (`registry.Add(">")`,
  seeing live traffic immediately without driving the Subscribe tab's `N` shortcut by hand) added
  alongside this change while manually verifying it via tmux; excluded from Release/AOT builds.
- No changes to `openspec/specs/shortcut-picker` — its own requirements (which shortcuts show,
  ordering, exclusion of top-level ones) are unaffected; this change's own spec delta
  (`shortcut-picker-launch`) covers the new reusable-mechanism requirements instead.
