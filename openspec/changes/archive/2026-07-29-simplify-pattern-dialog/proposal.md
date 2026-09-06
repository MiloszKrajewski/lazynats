## Why

`PatternDialog` (the single-`TextField` modal `SubscriptionsView` uses for Ctrl+N/Ctrl+E) carries a
Cancel/OK button pair for what is a one-field form: the `TextField` already owns Enter, and
`Dialog<TResult>` already treats Esc as cancel, so the buttons duplicate keyboard affordances the
dialog gets for free while adding a second focusable control to tab through. Separately, dialog
content currently sits flush against the border on both sides with no breathing room.

## What Changes

- `PatternDialog` drops its Cancel and OK `Button`s entirely. The `TextField`'s own `Accepted`
  (Enter) commits the trimmed pattern as `Result` and closes the dialog, but only while the pattern
  is valid (non-empty) — replacing the prior OK-button-disabled-while-invalid gating with "Enter is a
  no-op while invalid" (the same red-on-invalid `TextField` scheme still shows why). Esc cancels via
  `Dialog<TResult>`'s existing inherited behavior (`Result` stays unset) — unchanged from today, just
  no longer backed by an explicit Cancel button.
- `PatternDialog` sets `Padding.Thickness = new Thickness(1, 0, 1, 0)` in its own constructor, giving
  1-cell left/right space between the dialog's border and its content. This is set per-dialog, not
  via a shared base class or a `Dialog`-wide theme default — future custom `Dialog<T>` subclasses in
  this codebase repeat the same line rather than inheriting it from somewhere shared.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-subscriptions`: update the "Modifying a Subscription Pattern" requirement so the edit modal
  is specified as having no buttons — Enter commits a valid pattern, Esc cancels leaving the
  subscription unchanged (the latter previously had no explicit scenario).

## Impact

- `src/lazynats/Subscriptions/PatternDialog.cs`: remove `_okButton`, the Cancel `Button`, and both
  `AddButton` calls; wire commit directly off `_patternField.Accepted` gated on validity; add the
  `Padding.Thickness` line.
- `openspec/specs/nats-subscriptions/spec.md`: requirement update described above.
