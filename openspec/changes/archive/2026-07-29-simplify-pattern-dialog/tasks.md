## 1. `PatternDialog` buttons and padding

- [x] 1.1 Remove `_okButton`, the Cancel `Button`, and both `AddButton` calls from
      `src/lazynats/Subscriptions/PatternDialog.cs`.
- [x] 1.2 Wire `_patternField.Accepting` (not `Accepted` — see design.md's "Discovered while
      implementing" note) to commit: if the trimmed text is non-empty, set `Result` to it and call
      `RequestStop()`; otherwise no-op (dialog stays open, invalid scheme still shows via the
      existing `UpdateValidity()`). Always set `e.Handled = true` so an unhandled Enter doesn't bubble
      up to `Dialog<TResult>`'s own default accept handling.
- [x] 1.3 Remove the now-unused `_okButton` field/construction-order comment (the comment explained
      why `_okButton` had to exist before `ValueChanged` was wired — no longer applicable with no
      button).
- [x] 1.4 Add `Padding.Thickness = new Thickness(1, 0, 1, 0)` in the constructor.

## 2. Verification

- [x] 2.1 `dotnet build src/lazynats.sln` clean.
- [x] 2.2 Manually run the app against a local NATS server: Ctrl+N opens the modal, Enter with an
      empty field does nothing (field stays red, modal stays open), Enter with valid text commits and
      closes it, Esc closes it with no subscription change. Repeat for Ctrl+E on an existing
      subscription. If Esc does not close the dialog on its own (see design.md's open risk), add an
      explicit `Key.Esc` binding that calls `RequestStop()` without setting `Result`, then re-verify.
      Verified via a tmux-driven session against the already-running dockerized `nats-server` on
      `localhost:4222`: Esc closed the dialog unaided (no explicit binding needed) in both create and
      edit; empty-field Enter initially closed the dialog instead of no-op'ing (see design.md), fixed
      by switching to `Accepting`/`e.Handled = true`, then re-verified clean for both create and edit.
- [x] 2.3 Visually confirm 1-cell left/right padding between the dialog border and the
      label/`TextField`. Confirmed (`┃ Pattern` / `┃ invoices.>` — one space before the border on
      rows not affected by the pre-existing redraw quirk noted in design.md).

## 3. Finalize

- [x] 3.1 Run `openspec validate simplify-pattern-dialog` before archiving.
