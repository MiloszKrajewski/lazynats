## Context

`ShortcutAggregator.Collect(focused)` already walks the focused view's ancestor chain collecting
`IShortcutSource.Shortcuts`, and `ShortcutTracker` pushes a recomputed list into
`MainWindow.SyncDynamicShortcuts` on every focus change, which tears down and rebuilds the status
bar's dynamic tail. That push model exists solely to keep the status bar in sync — with the
status bar no longer showing per-view shortcuts, nothing needs a continuously updated push
anymore; the aggregation only needs to be read once, at the moment the picker opens.

The codebase already has a working pattern for "modal dialog that returns a selection and closes
on Enter/Esc": `Dialog<T>` subclasses (`PatternDialog`, `CreateStreamDialog`,
`CreateConsumerDialog`, `CreateBucketDialog`). Terminal.Gui's modal `Application.Run(dialog)`
already prevents the background toplevel's key bindings from firing while the dialog runs, which
is exactly the "underlying shortcut key doesn't fire while the picker is open" behavior wanted —
no new key-routing mechanism is needed.

## Goals / Non-Goals

**Goals:**
- Fixed, hardcoded top-level shortcut set in the status bar (`Alt-1..4`, `Alt-P`, `Alt-Q`,
  `Alt-K`).
- `Alt+K` opens a modal `ShortcutPickerDialog` listing `ShortcutAggregator.Collect(...)` for the
  current focus chain, alphabetical by name — deliberately excluding the hardcoded top-level set
  (see Decisions), since those are already permanently visible in the status bar.
- Enter closes the dialog, then invokes the selected shortcut's action.
- Esc closes the dialog, no action invoked.
- Remove the now-unnecessary continuous push path (`ShortcutTracker.ShortcutsChanged` →
  `MainWindow.SyncDynamicShortcuts`) and the status-bar rebuild churn it caused.

**Non-Goals:**
- Shortcut discovery while a different modal (e.g. `CreateStreamDialog`) is the active toplevel.
- `/`-triggered fuzzy filtering inside the picker (noted as future work).
- Grouping/categorization of entries (noted as future work).
- Changing the `IShortcutSource` contract or any individual view's advertised shortcuts (label
  wording improvements are left to each view, independently of this change).

## Decisions

**Hardcoded top-level list stays a separate literal, not an `IShortcutSource`.** Confirmed with
the user: rather than promoting `MainWindow`'s fixed `Alt-1..4`/`Alt-P`/`Alt-Q`/`Alt-K` into the
`IShortcutSource` contract, keep it exactly as it is today — a small hardcoded array/list of
`ShortcutHint`s constructed alongside the existing status-bar `Shortcut` widgets in `MainWindow`.

**Picker excludes the hardcoded top-level set entirely.** Originally the picker's data was
`hardcodedTopLevel.Concat(ShortcutAggregator.Collect(focused))`. Revisited with the user after
shipping: those shortcuts are already permanently visible in the status bar, unlike the per-view
ones the picker exists to surface because they don't fit there — listing them a second time in
the picker is redundant. Changed to just `ShortcutAggregator.Collect(focused)`; `topLevelShortcuts`
still exists unchanged for the status bar display and the `MainWindow.KeyDown` routing below, it
just no longer feeds the picker. This makes an empty picker (a focused view advertising nothing
of its own, e.g. the live feed) reachable where it previously never was — handled with a "No
shortcuts for this view" empty-state message rather than a blank dialog.

**The picker's `ListView` uses `TabStop = TabBehavior.NoStop`, not the default.** Found via a
user bug report: pressing Up on the first row dimmed the highlight (the list had lost focus) and
Enter afterward closed the dialog without running anything. Root cause is a documented
Terminal.Gui behavior — `ListView.MoveUp`/`MoveDown` only wrap the selection at the list's edges
when `TabStop == TabBehavior.NoStop`; with the default `TabStop`, an edge Up/Down is left
unhandled and bubbles further up, where some other focusable element in this buttonless
`Dialog<T>` claims focus instead (exactly the "invisible control" the user correctly guessed at).
Fixed by opting the list out of the Tab-stop protocol entirely — there's nothing else in this
dialog to Tab to anyway, so it costs nothing, and Up/Down at the edges now wrap within the list
as expected. Confirmed via `tmux` with a temporary diagnostic (see tasks.md 6.4).

**Remove `ShortcutTracker`'s push model; compute on demand instead.** `ShortcutTracker` today
subscribes to `_app.Navigation.FocusedChanged` purely to keep the status bar's dynamic tail in
sync. With that consumer gone, the picker's trigger handler calls
`ShortcutAggregator.Collect(_app.TopRunnableView?.MostFocused)` directly at the moment the dialog
opens — a snapshot, not a subscription. `ShortcutTracker` itself (the `FocusedChanged`
subscription, `ShortcutsChanged` event, `Refresh()`) is deleted rather than kept as dead
infrastructure; `ShortcutAggregator.Collect` (the pure walk) is the only piece that survives.
Alternative considered: keep `ShortcutTracker` and have the picker read a cached
last-known-good list — rejected because the picker only needs the *current* focus at open time,
a cache adds a staleness risk (focus could move via non-`FocusedChanged` paths between updates)
for no benefit, and it deletes the comment-documented focus-transition rebuild hazard in
`MainWindow.SyncDynamicShortcuts` for free.

Removing `ShortcutTracker` has a second call site beyond `MainWindow`: `ListEditorView` holds its
own `ShortcutTracker` (via DI) and calls `_shortcutTracker.Refresh()` after `New`/`Edit` complete,
purely to force the status bar to resync when the view's own advertised shortcuts change without
a focus change. Once nothing renders the aggregation continuously, that `Refresh()` call has
nothing left to notify — it becomes dead code and is removed along with the field, not left
in place as an inert no-op.

**Sort by name (label text), not key chord.** Matches how a user scans for a command by what it
does, not by which key someone bound it to. Ordinal, case-insensitive comparison. Confirmed with
the user, who also noted labels can now be more descriptive since they no longer share the
status bar with five other entries — that wording pass is left to each view, not part of this
change.

**Enter closes the dialog, then invokes the action ("close-then-invoke").** Confirmed with the
user as the safer default: the picker's own modal loop is fully unwound before the selected
shortcut's `Action` runs, so an action that itself opens another dialog (e.g. `Alt-P`'s publish
flow, if ever added to the picker's reachable set) never has to reason about running underneath
a still-active picker modal.

**No new key-suppression mechanism for ordinary per-view shortcuts.** `ShortcutPickerDialog` is an
ordinary `Dialog<ShortcutHint>` run via the existing modal `Application.Run` path used by every
other dialog in the app. This is what makes "the underlying view's shortcut key doesn't fire while
the picker is open" true for ordinary `KeyBindings`-based shortcuts (Ctrl+R/N/E/D, ...) without
writing any bespoke key-interception code. The hardcoded top-level set needed a separate fix,
below — it wasn't using this mechanism at all.

**`BindKeyToApplication` removed from the whole top-level set; replaced with a `MainWindow.KeyDown`
handler.** Originally assumed (wrongly — see below) that wiring the picker's trigger the same way
`Alt-1..4` already were (`BindKeyToApplication = true` on a `StatusBar` `Shortcut`) would inherit "doesn't
fire while a modal is open" for free, matching every other dialog. Live tmux verification proved
the opposite: `BindKeyToApplication` binds the key at the `Application` level, bypassing normal
modal key-routing entirely. Confirmed two concrete failures before the fix: (1) pressing `Alt+3`
while the picker sat open on the Streams tab silently switched the background to the Values tab,
leaving the picker open on top showing now-stale, mismatched entries; (2) pressing the picker's
own trigger key (tested as `F2`) again while it was already open re-fired the same global binding
and stacked a second `ShortcutPickerDialog` on top of the first — worse than (1), since it's
self-reentrant and would keep stacking on repeated presses.

Raised to the user, who dislikes `BindKeyToApplication` as a mechanism on principle; resolved by
removing it from the *entire* top-level set (`Alt-1..4`, `Alt-P`, `Alt-Q`, `Alt-K`), not just the
new entry. Replacement: `MainWindow` subscribes to its own `KeyDown` event (raised bottom-up along
the focused view's ancestor chain, before key bindings are invoked) and matches the pressed key
against `topLevelShortcuts` directly, calling the matching hint's `Action` and setting
`key.Handled = true`. Since every tab's content is a `MainWindow` descendant, this still fires
regardless of which tab/list has focus (same bubbling `ListEditorView`'s own Ctrl+N/E/D already
rely on) — but a modal `Dialog` run via `App!.Run(dialog)` is a separate top-level session with no
`SuperView` link back to `MainWindow`, so an unhandled key inside one never reaches this handler
at all. Verified via tmux (see tasks.md 5.6): `Alt+3` no longer switches tabs while the picker is
open, and re-pressing the trigger key while it's open no longer stacks a second dialog — with zero
picker-specific special-casing needed. The `Shortcut` widgets in the status bar keep their
`Key`/`Text`/`Action` for display and mouse-click support, just without
`BindKeyToApplication`.

This is a deliberate behavior change beyond this proposal's original scope: today `Alt-1..4`/
`Alt-P`/`Alt-Q` already interrupt any other already-open dialog (`CreateStreamDialog`,
`PublishDialog`, ...) too, via the same `BindKeyToApplication` mechanism this change removes
project-wide, not just for the picker. Confirmed with the user as the intended, wider fix rather
than a picker-only guard.

**Trigger key iterated to `Alt+K` after real-terminal testing.** `Ctrl+/` (the original choice)
turned out to do nothing on the user's actual Windows terminal — confirmed by Terminal.Gui's own
default key bindings, which explicitly exclude `Ctrl+/` on Windows (their built-in Undo binding
is `Bind.AllPlus("Ctrl+Z", nonWindows: ["Ctrl+/"])`); Windows' console input model doesn't reliably
produce a distinguishable event for Ctrl held with a punctuation key. `tmux`-based testing during
implementation had not caught this, since `tmux`'s own delivery of raw `Ctrl+<key>` bytes through
this git-bash/Windows stack was independently broken for unrelated reasons and had masked the
platform issue rather than exposing it. `Alt+/` (tried next) fared no better on the user's
terminal, nor via `tmux`. `F1` worked on both, but function keys are unreliable on some laptop
keyboards (Fn-lock). Settled on `Alt+K` — matches the rest of the top-level set's `Alt+<letter>`
shape, confirmed working via `tmux` and by the user directly.

## Risks / Trade-offs

- **Two hardcoded/aggregated lists could drift** (someone adds a new top-level global shortcut to
  the status bar but forgets the picker, or vice versa) → Mitigation: build both from the same
  point in `MainWindow` (construct the hardcoded `ShortcutHint` list first, derive both the
  status-bar `Shortcut` widgets and the picker's static half from it) so there's one array to
  edit even though it's not routed through `IShortcutSource`.
- **Removing `ShortcutTracker` could break an undiscovered second subscriber** →  Mitigation:
  confirmed via grep that `MainWindow.SyncDynamicShortcuts` is the only subscriber to
  `ShortcutsChanged`, and that `ListEditorView`'s `Refresh()` calls (see Decisions) are the only
  other usage — both are removed together, not left partially wired.
- **Name collisions across sources** (two different shortcuts in the same aggregated list sharing
  a label) → Not mitigated beyond what the old status bar already risked (same aggregation, same
  potential for a confusing label); the key chord is still shown alongside the name in each row
  so a collision is visible/disambiguable, not silently merged.

## Migration Plan

No data migration; this is a UI-only behavior change in a single-user desktop app. Rollout is
the implementation itself:
1. Add `ShortcutPickerDialog`.
2. Wire `Alt+K` in `MainWindow` alongside the other hardcoded status-bar shortcuts.
3. Remove `ShortcutTracker.ShortcutsChanged` wiring and `SyncDynamicShortcuts` from `MainWindow`.
4. Remove `ListEditorView`'s `_shortcutTracker` field and its two post-modal `Refresh()` calls,
   then delete `ShortcutTracker` and its DI registration in `Program.cs`.
5. Manually verify via `tmux` (per `CLAUDE.md`'s driving instructions): status bar shows only the
   fixed set on every tab, `Alt+K` opens the picker with the right entries per focused view,
   Enter runs the selected action, Esc is a no-op, and the underlying view's own shortcut key is
   inert while the picker is open.

Rollback is a plain revert; no persisted state is involved.

## Open Questions

- Exact `ShortcutPickerDialog` title text/footer hint wording (e.g. `" Shortcuts "`, with an
  Enter/Esc hint row) — cosmetic, resolve during implementation rather than here.
- Whether any existing `IShortcutSource` already advertises the new global binding's key (which
  would collide) — check via grep before wiring each candidate key. Resolved for all four tried
  (`Ctrl-/`, `Alt-/`, `F1`, final `Alt-K`): no collision found for any of them (only bare `/` for
  Find/Search exists today, unrelated to any of these).
