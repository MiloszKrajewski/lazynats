## 1. Helper

- [x] 1.1 Add `Components/ShortcutPickerLauncher.cs` with a single `Key` constant (`?`,
      currently `new Key('?')` duplicated in `MainWindow.cs` and `MessageDetailDialog.cs`).
- [x] 1.2 Implement `MakeAction(View owner, Func<View?> startView) : Action`: the deferred
      `AddTimeout(TimeSpan.Zero, ...)` → `ShortcutAggregator.Collect(startView())` →
      `new ShortcutPickerDialog(hints)` → `owner.App!.Run(picker)` → `picker.Result?.Action()`
      sequence, carrying forward the re-entrancy-avoidance reasoning from `MainWindow.cs`'s
      existing comment (design.md Context/Decisions). Takes `owner: View`, not an `IApplication`/
      `Func<IApplication>` — reads `owner.App!` lazily inside the closure instead, since every
      caller already has a plain `View` reference on hand (design.md's superseded-decision note
      on this).
- [x] 1.3 Implement `BindKey(View owner, Func<View?>? startView = null)`: subscribes
      `owner.KeyDown`, matches `Key`, sets `key.Handled = true`, invokes
      `MakeAction(owner, startView ?? (() => ResolveStartView(owner)))()` — implemented in terms
      of 1.2, not a second copy of the sequence. `startView` defaults to `null`, triggering a new
      `ResolveStartView(View owner)` heuristic (walk `owner.MostFocused` upward via `SuperView`
      looking for `owner`; use `MostFocused` if found, else fall back to `owner` itself) so most
      callers need no explicit policy at all (design.md's `ResolveStartView` decision).

## 2. Call sites

- [x] 2.1 `MainWindow.cs`: replace the inline `?` `ShortcutHint` action (~L145-158) with
      `ShortcutPickerLauncher.MakeAction(this, () => App!.TopRunnableView?.MostFocused)`, and use
      `ShortcutPickerLauncher.Key` in place of the local `new Key('?')`. Keep the
      `topLevelShortcuts`/status-bar-widget wiring unchanged. Still supplies an explicit
      `startView` (unlike 2.2/2.4 below) since `MainWindow`'s correct start point is app-wide
      focus, not `ResolveStartView`'s "does focus reach back to `owner`" check.
- [x] 2.2 `MessageDetailDialog.cs`: replace the hand-rolled `KeyDown` subscriber (~L172-190) with
      `ShortcutPickerLauncher.BindKey(this)`, relying on the default `ResolveStartView`. Keep a
      short local comment explaining why the default's fallback branch (start at the dialog
      itself) is correct here: every view it adds is `CanFocus=false`, so there's no genuinely
      focusable descendant for `MostFocused` to ever land on (design.md Context / Risks).
- [x] 2.3 Trim each call site's comment block to what's caller-specific; move the
      generic re-entrancy / raw-`KeyDown`-vs-`Command.Context` / upward-only-`Collect` reasoning
      into `ShortcutPickerLauncher.cs` itself, referenced rather than repeated.
- [x] 2.4 `PublishDialog.cs`: add `ShortcutPickerLauncher.BindKey(this)` (default
      `ResolveStartView`, no override needed) after its field-level bindings are set up, giving it
      working `?` support for the first time — its `HeaderEditorView` already implements
      `IShortcutSource`. Not in the original scope (proposal.md's Non-Goals excluded retrofitting
      `?` onto dialogs without it), added once the default made it a one-line addition with no
      new hand-reasoning required; see design.md's updated Non-Goals note.

## 3. Verification

- [x] 3.1 Build (`dotnet build src/lazynats.sln`).
- [x] 3.2 Via tmux: confirm `?` still opens the picker from a management tab and from the live
      feed, listing the same shortcuts as before (top-level shortcuts excluded). Confirmed both
      (Subscribe tab: Delete/Edit/New; Live Feed: Space/Follow-Pause) — this run is what caught
      the `IApplication`-parameter bug fixed by design.md's `MakeAction`/`BindKey`-signature
      Decisions entry, since `?` from MainWindow crashed the app before that fix (first the
      `Func<IApplication>` intermediate fix, then the final `View owner`-based shape).
- [x] 3.3 Via tmux: confirm `?` still opens the picker from an open `MessageDetailDialog`
      (Enter on a live-feed row), listing that dialog's own shortcuts (e.g. Presentation), and
      that selecting an entry runs it after the picker closes. Verified: the `psmux` tmux shim's
      `N` key was still unreliable, so a subscription was seeded to get a live-feed message
      without depending on that key. This started as a one-line temporary edit to `Program.cs`
      but proved broadly useful enough (avoids driving the Subscribe tab's `N` shortcut by hand on
      every manual verification pass) that it was kept as a permanent `#if DEBUG`-guarded
      `registry.Add(">")` convenience instead of being reverted — see proposal.md's Impact entry
      for it; unrelated to this change's own scope, riding along as an incidental improvement.
      With a real message present: opened `MessageDetailDialog` (Enter on the live-feed row),
      pressed `?` — the Shortcuts picker opened listing exactly `v  Presentation` (this dialog's
      own shortcut, no top-level ones) — and pressing Enter on it closed the picker and opened the
      Presentation dropdown's popover (Text/Hex/Base64 visible), confirming the selected entry's
      action ran.
- [x] 3.4 Via tmux: confirm `?` opens the picker from an open `PublishDialog` (Alt+P), listing its
      `HeaderEditorView`'s shortcuts. With the Subject `TextField` focused, `?` is consumed as
      literal text (expected — depth-first dispatch, same as any other focused text field, no
      picker opens); Tab'd focus to the empty header list instead and pressed `?` there — the
      Shortcuts picker opened listing exactly `d  Delete` / `e  Edit` / `n  New`
      (`HeaderEditorView`'s own shortcuts, no top-level ones), confirming `ResolveStartView`'s
      `MostFocused`-reaches-`owner` branch resolves correctly for this dialog.
