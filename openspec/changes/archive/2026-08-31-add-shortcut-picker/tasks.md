## 1. Shortcut Picker Dialog

- [x] 1.1 Add `ShortcutPickerDialog : Dialog<ShortcutHint>` (e.g. under `Components/`), following
      the existing `Dialog<T>` pattern (`PatternDialog`): a `ListView` populated with the
      supplied `IReadOnlyList<ShortcutHint>` entries, sorted alphabetically (case-insensitive) by
      `Text`, each row showing name + key chord. Actual type is `Dialog<ShortcutHint?>`, not
      `Dialog<ShortcutHint>` - see 1.2's note. Also needed, found only via live tmux testing: (a)
      an explicit `listView.SelectedItem = 0` after `SetSource` - a fresh `ListView` starts with
      no selection, so Enter before ever navigating invoked nothing; (b) an explicit
      `listView.SetFocus()` after `Add` - a buttonless `Dialog<T>` doesn't auto-focus non-button
      content the way `PatternDialog`'s field does.
- [x] 1.2 Wire Enter on the list to set `Result` to the highlighted entry and call `RequestStop()`
      (close-then-invoke: the actual `Action` invocation happens in the caller, after
      `App!.Run(dialog)` returns — the dialog itself only returns the selection). Bound via
      `Accepting` (pre-event) with `e.Handled = true`, not `Accepted` (post) - found via tmux
      testing that without `e.Handled = true`, the same Enter keypress also bubbles to `Dialog<T>`'s
      own default accept handling, which clobbers the just-set `Result` back to null (same
      reasoning as `PatternDialog`'s field handler and `CreateStreamDialog`'s Create button, both
      of which already do this). Also switched `TResult` from `ShortcutHint` to `ShortcutHint?`:
      for a non-nullable value type, `Dialog<T>`'s "Result stays null on cancel" convention needs
      the nullable form, or Esc would be indistinguishable from a real (default) selection.
- [x] 1.3 Confirm Esc's inherited `Dialog<T>` cancel behavior leaves `Result` unset/null and
      invokes nothing — no extra handling needed if the base class already does this (verify
      against `PatternDialog`'s comment about Esc/cancellation convention).
- [x] 1.4 Give the dialog a bordered title following the `" Title "` padding convention (e.g.
      `" Shortcuts "`).

## 2. Hardcoded Top-Level Shortcut List

- [x] 2.1 In `MainWindow`, factor the existing top-level `Shortcut` widgets' key/text/action
      triples (`Alt-1..4`, `Alt-P`, `Alt-Q`) into a single place they're both defined from, so the
      status bar and the picker's hardcoded half read from the same data instead of two
      hand-maintained lists.
- [x] 2.2 Add a `Ctrl-/` entry (name TBD, e.g. `"Shortcuts"`) to that same hardcoded set, opening
      `ShortcutPickerDialog`. **Not** wired with `BindKeyToApplication = true` — see 2.4, added
      after live testing showed that mechanism doesn't respect modal scoping at all.
- [x] 2.3 Grep the codebase for any existing `IShortcutSource.Shortcuts` binding `Ctrl-/` (or
      `Key.Oem2`/whatever Terminal.Gui's key constant for `/` is combined with Ctrl) to rule out a
      collision before wiring it globally. Confirmed: only bare `/` (Find/Search) exists today, no
      `Ctrl-/` collision.
- [x] 2.4 **Added during implementation** (not in the original plan — see design.md's corrected
      "`BindKeyToApplication` removed..." decision): removed `BindKeyToApplication = true` from
      every entry in the top-level set, not just the new one. Live tmux testing proved it bypasses
      modal key-routing entirely, letting e.g. `Alt+3` silently switch tabs — or the picker's own
      trigger key re-fire and stack a second dialog — while `ShortcutPickerDialog` sat open on top.
      Raised to the user (who dislikes `BindKeyToApplication` on principle); resolved by
      subscribing to `MainWindow`'s own `KeyDown` event and matching against `topLevelShortcuts`
      directly instead — fires while `MainWindow` is part of the key-dispatch chain (any tab, any
      focus), not while a modal `Dialog` (a separate top-level session) is running. The `Shortcut`
      widgets in the status bar keep `Key`/`Text`/`Action` for display and mouse-click support,
      just without `BindKeyToApplication`. This is a deliberate, confirmed-with-the-user behavior
      change beyond the picker itself: `Alt-1..4`/`Alt-P`/`Alt-Q` no longer interrupt any other
      already-open dialog (`CreateStreamDialog`, `PublishDialog`, ...) either, project-wide.

## 3. Wire the Picker Into MainWindow

- [x] 3.1 In the `Ctrl-/` shortcut's `Action`, compute
      `hardcodedTopLevel.Concat(ShortcutAggregator.Collect(App!.TopRunnableView?.MostFocused))`,
      pass it into a new `ShortcutPickerDialog`, run it modally (`App!.Run(dialog)`), and — only
      after `Run` returns — invoke `dialog.Result?.Action`.
- [x] 3.2 Follow the same `AddTimeout(TimeSpan.Zero, ...)` deferral pattern already used by
      `publishShortcut.Action` if the same re-entrancy hazard (nested `Run()` re-observing the
      in-flight Ctrl+/ keypress) applies here — verify by testing, don't assume. Applied
      defensively (structurally identical to Publish's hazard); confirmed via `tmux` in 5.3.

## 4. Remove the Dynamic-Tail Push Path

- [x] 4.1 Remove `_shortcutTracker` field, `ShortcutsChanged` subscription, `SyncDynamicShortcuts`,
      `_dynamicShortcuts`, and `_staticShortcutCount` from `MainWindow`; the `StatusBar` is now
      constructed once with only the fixed shortcut set.
- [x] 4.2 Remove `ListEditorView`'s `_shortcutTracker` field and the two post-`New`/`Edit`
      `_shortcutTracker.Refresh()` calls — they have no remaining consumer once the status bar no
      longer renders the aggregation continuously.
- [x] 4.3 Delete the `ShortcutTracker` class from `Components/ShortcutAggregator.cs`, keeping
      `ShortcutAggregator.Collect` (still used by the picker) and `ShortcutHint`/`IShortcutSource`
      untouched.
- [x] 4.4 Remove `ShortcutTracker`'s DI registration and the comment explaining its
      construction-order requirement in `Program.cs`; confirm `Application.Create()`'s ordering
      relative to `Services.Configure()` no longer needs special justification once nothing needs
      a live `IApplication` at construction time for this purpose (re-check other DI
      registrations aren't relying on that same ordering for an unrelated reason before touching
      it). Kept the ordering itself (`services.AddSingleton(app)` still needs `app` to exist first)
      but dropped the now-inapplicable comment.

## 5. Verification

- [x] 5.1 Build (`dotnet build src/lazynats.sln`) and confirm no leftover references to
      `ShortcutTracker`/`SyncDynamicShortcuts`. Clean build, 0 warnings/errors.
- [x] 5.2 Using `tmux` per `CLAUDE.md`'s driving instructions: confirm the status bar shows only
      the fixed set on every tab (no dynamic tail appears when focusing e.g. a `ListEditorView` or
      `DrillableListView`). Confirmed: bar reads `Alt+Q Quit | Alt+1 Subscribe | Alt+2 Streams |
      Alt+3 Values | Alt+4 Objects | Alt+P Publish | Ctrl+/ Shortcuts` on every tab regardless of
      which list/view has focus.
- [x] 5.3 Via `tmux`: press Ctrl+/ (tested via a temporary `F2` substitute key - direct `Ctrl+/`
      delivery isn't reachable through this tmux/Windows stack; see finding below) from each tab
      and the live feed; confirm the picker lists the hardcoded set plus that view's advertised
      shortcuts, alphabetically by name. Confirmed on Subscribe (10 entries incl. New/Edit/Delete)
      and Streams (12 entries incl. Refresh/Search/New/Edit/Delete), both correctly alphabetized.
- [x] 5.4 Via `tmux`: highlight an entry and press Enter; confirm the picker closes and the
      action runs (e.g. picking a tab-switch entry actually switches tabs). Confirmed: navigated
      to "Objects" and separately to "Streams" by index, Enter closed the dialog and switched to
      the correct tab both times. Two real bugs were found and fixed while getting this to work -
      see updated notes on 1.1/1.2.
- [x] 5.5 Via `tmux`: open the picker, press Esc; confirm it closes and nothing was triggered.
      Confirmed both from a fresh selection and after navigating - dialog closes, no action runs,
      background state unchanged.
- [x] 5.6 Via `tmux`: open the picker while a view exposing e.g. `Ctrl-R` (refresh) is
      focused; confirm pressing `Ctrl-R` while the picker is open does not trigger the refresh,
      and that it still works normally once the picker is closed.
      - Ordinary per-view `KeyBindings`-based shortcuts (Ctrl+R/N/E/D, the same mechanism every
        existing `Dialog<T>` in the app already relies on to block background input): inferred,
        not directly observed — raw Ctrl+<letter> bytes don't reach this app through this
        tmux/Windows stack at all, including pre-existing `Ctrl+N`. The picker's own Down/Enter
        routing (proven working, see 5.4) rules out background double-navigation, consistent with
        normal modal scoping holding; this path is unchanged by this task's work.
      - The hardcoded top-level `BindKeyToApplication` shortcuts: **directly observed failing**
        (`Alt+3` pressed while the picker sat open on the Streams tab silently switched the
        background to the Values tab; re-pressing the picker's own trigger key while it was open
        stacked a second dialog), then **fixed and re-verified** after removing
        `BindKeyToApplication` project-wide in favor of `MainWindow.KeyDown` (see 2.4): `Alt+3`
        pressed while the picker was open no longer changed tabs, and re-pressing the trigger key
        while it was open no longer opened a second dialog. See design.md's corrected
        "`BindKeyToApplication` removed..." decision for the full account.

## 6. Post-Ship Fixes (User-Reported, After Real-Terminal Testing)

- [x] 6.1 **Trigger key iterated**: the user reported `Ctrl+/` did nothing on their real Windows
      terminal. Root cause confirmed via Terminal.Gui's own docs: their built-in key bindings
      explicitly exclude `Ctrl+/` on Windows (`Bind.AllPlus("Ctrl+Z", nonWindows: ["Ctrl+/"])`) —
      Windows' console input model doesn't reliably produce a distinguishable event for Ctrl held
      with a punctuation key. `tmux`-based testing during implementation hadn't caught this because
      `tmux`'s own `Ctrl+<key>` delivery through this stack was independently broken, masking the
      platform issue rather than exposing it. Tried `Alt+/` next — also confirmed dead on the
      user's terminal (and, this time, via `tmux` too). Tried `F1` — worked on both, but the user
      flagged function keys as unreliable on some laptop keyboards (Fn-lock). Settled on `Alt+K` —
      matches the rest of the top-level set's `Alt+<letter>` shape, confirmed working via `tmux`
      and directly by the user. Updated the `Key.F1`/`new Key('/').With...` line and its
      explanatory comment in `MainWindow.cs` each time; final state uses `Key.K.WithAlt`.
- [x] 6.2 **Picker contents narrowed**: at the user's request, the picker no longer lists the
      hardcoded top-level shortcuts (`Alt-1..4`, `Alt-P`, `Alt-Q`, `Alt-K` itself) — only
      `ShortcutAggregator.Collect(focused)`, since the top-level set is already permanently
      visible in the status bar and repeating it in the picker was redundant. `topLevelShortcuts`
      itself is unchanged (still backs the status bar and `MainWindow.KeyDown`), it just no longer
      feeds `ShortcutPickerDialog`'s constructor.
- [x] 6.3 **Empty-state handling added**: narrowing the picker's contents (6.2) made an
      all-empty list reachable for the first time (a focused view advertising no shortcuts of its
      own, e.g. the live feed, previously always got at least the top-level set). Added a
      "No shortcuts for this view" `Label` shown instead of the `ListView` when the aggregated
      list is empty; confirmed via `tmux` by tabbing focus into the live feed and opening the
      picker. Esc still closes it normally (inherited `Dialog<T>` behavior, unaffected by content).
- [x] 6.4 **User-reported bug fixed: Up from the first row silently steals focus.** The user
      reported that pressing Up while the first row was highlighted dimmed the highlight (a sign
      the list had lost focus) and that Enter afterwards closed the dialog but ran nothing —
      correctly guessing "one more focusable but invisible control." Root cause, confirmed via
      `tmux` with a temporary diagnostic (`Title` updated on `ListView.ValueChanged` to show
      `SelectedItem`/`HasFocus`): `ListView.MoveUp`/`MoveDown` only wrap the selection at the list's
      edges when `TabStop == TabBehavior.NoStop` (documented Terminal.Gui behavior); with the
      default `TabStop`, Up-at-the-top is left unhandled and bubbles further up, where some other
      focusable element in this buttonless `Dialog<T>` picks it up, taking focus (and all
      subsequent Enter/Accepting handling) away from the list. Fixed by setting
      `listView.TabStop = TabBehavior.NoStop` — there's nothing else in this dialog to Tab to
      anyway, so opting out of the Tab-stop protocol costs nothing. Re-verified via `tmux`: Up from
      the first row now wraps to the last row (diagnostic confirmed `idx=2 focus=True` on a 3-item
      list), and Enter on the wrapped-to entry correctly invokes it (opened `PatternDialog` for
      "New"). Down-wrap at the bottom exercised too (lands back on the confirmed-safe "Delete"
      no-op on an empty list) with no regression to normal navigation or app responsiveness
      afterward. Diagnostic code removed before finalizing.
