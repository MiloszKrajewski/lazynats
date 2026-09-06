## Context

`PatternDialog` (`src/lazynats/Subscriptions/PatternDialog.cs`, from the recently archived
`simplify-list-editor-modal` change) is a `Dialog<string>` with one `TextField`, a Cancel `Button`,
and an OK `Button` whose `Enabled` is gated on the field being non-empty. `Dialog<TResult>` derives
from `Runnable<TResult>` → `Runnable`, the same ancestry `MainWindow` (a `Window` : `Runnable`) has —
and `doc/terminal-gui-howto.md` documents "Quit a running app with Esc" for that ancestry without
`MainWindow` wiring anything explicit, implying Esc-to-`RequestStop` is inherited behavior on
`Runnable`, not something each subclass adds. `PatternDialog` today never exercises that path bare:
its Cancel button's own `Accepting` handler calls `RequestStop()` first.

## Goals / Non-Goals

**Goals:**
- Remove `PatternDialog`'s two `Button`s; commit-on-Enter and cancel-on-Esc become the entire
  interaction, with Enter reusing the field's existing validity check instead of a disabled button.
- Give `PatternDialog` 1-cell left/right padding between its border and the `TextField`/`Label`.

**Non-Goals:**
- No shared "single-field dialog" base class. Per explicit preference, each custom `Dialog<T>` this
  codebase adds later repeats its own `Padding.Thickness` line rather than inheriting one — cheapest
  option now, revisit only if/when a third dialog makes the duplication actually annoying.
- No changes to `ListEditorView<T>`, `SubscriptionsView`, or the `TryCreate`/`TryEdit`/`Add`/
  `Replace` plumbing — `PatternDialog` is still constructed and run exactly as it is today; only its
  own internals (buttons, padding) change.
- No theme/color changes (the separate "why is the dialog blue" / "prefer black" thread from
  exploration is not addressed by this change).

## Decisions

### Rely on `Dialog<TResult>`'s inherited Esc-cancel, don't add an explicit Esc key binding
Removing the Cancel button removes the only place that currently calls `RequestStop()` on cancel.
Rather than re-adding that via an explicit `KeyBindings.Add(Key.Esc, ...)`, this relies on the
inherited `Runnable` behavior referenced above. **Confirmed by manual verification**: run against a
live NATS server via a tmux-driven session, Esc closed both the create and edit modal with the
subscription list left unchanged in both cases — no explicit binding needed.

### Enter is a no-op while invalid, not "OK disabled while invalid"
The existing `UpdateValidity()` already flips `_patternField`'s scheme to the red/invalid `Attribute`
when the trimmed text is empty. With no OK button to disable, `_patternField`'s commit handler
becomes the sole commit path and checks the same validity condition itself before setting `Result`
and calling `RequestStop()`; if invalid, the handler simply returns and the dialog stays open with
the field still showing red — same visual feedback as before, one fewer control involved in
producing it.

**Discovered while implementing:** wiring this off `Accepted` (post-event, doesn't stop propagation)
instead of `Accepting` (pre-event, `e.Handled = true` stops it) let an empty-field Enter bubble past
the `TextField` to `Dialog<TResult>`'s own default accept handling, which closed the dialog anyway
(with `Result` still unset, so no bad data — but the modal closed instead of staying open as
intended). With no button left to consume the keypress, `PatternDialog` itself has to be the one
that marks it handled. Fixed by moving the commit logic to `_patternField.Accepting` and always
setting `e.Handled = true` (both on the valid-commit path and the invalid-no-op path) — verified live
after the fix: empty Enter now leaves the modal open, confirmed for both the create and edit dialogs.

### Padding set directly in `PatternDialog`'s constructor
`Padding.Thickness = new Thickness(1, 0, 1, 0)` (left/right 1, top/bottom 0) is set as a plain
statement in the constructor, alongside the existing field/label setup — no extraction, no shared
default. Matches the Non-Goals call above.

## Risks / Trade-offs

- **No shared base means padding/no-button conventions must be remembered per-dialog** → accepted
  trade-off (explicit preference for less code now over a reusable abstraction with only one real
  caller today).

## Pre-existing issue observed, out of scope

While verifying live, typing into `_patternField` sometimes redraws the dialog's right border one
cell past the end of the just-typed text, instead of at its actual column (e.g. after typing
`invoices.>` the row briefly renders as `┃ invoices.> ┃                    ┃` — a `┃` appearing right
after the text, in addition to the real border further right). Reproduced identically with the new
`Padding.Thickness` line commented out, so it predates this change and isn't caused by padding or by
removing the buttons — looks like a `TextField`/`Dialog` incremental-redraw quirk in this Terminal.Gui
version, cosmetic only (doesn't affect `Result`/commit correctness, and clears on the next full
redraw). Not fixed here; flagging in case it's worth its own investigation later.

## Migration Plan

Single-PR, no persisted state, no rollback concerns beyond reverting the commit:
1. Remove `PatternDialog`'s buttons; wire commit through `_patternField.Accepted`.
2. Verify Esc-cancel still works; add an explicit binding only if the inherited behavior doesn't
   hold.
3. Add the `Padding.Thickness` line.
4. Manually re-verify Ctrl+N/Ctrl+E/Ctrl+D in the Subscribe tab against a live NATS server (no
   automated UI test project exists yet), matching the verification style of the prior
   `simplify-list-editor-modal` change.
