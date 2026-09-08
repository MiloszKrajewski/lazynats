## Why

`CreateKeyDialog` (the "New Key"/"Edit Key" modal used by the KV Values tab) is fixed at a
narrow 43-column width with a 10-row Value field, regardless of terminal size. Editing or
authoring a multi-line KV value in that cramped, non-scrolling-friendly box is awkward compared
to the read-only `ValueDetailDialog`, which already scales its width up to the available screen.

## What Changes

- `CreateKeyDialog`'s width becomes responsive to the terminal's current size (same
  `Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin))` pattern
  `ValueDetailDialog` already uses), instead of a fixed 43 columns.
- The Value field's height becomes responsive to the terminal's current height (capped at a
  maximum), instead of a fixed 10 rows, so more of the value is visible while editing without
  scrolling on larger terminals.
- The Name field stays a fixed, single-line height (3 rows including its frame) - only width and
  the Value field's height grow.
- No change to field validation, edit/create semantics, or keybindings - purely a sizing change.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-kv`: "Create Key" and "Edit Key" requirements' dialog is described as opening "a modal
  dialog collecting Name and Value (a multi-line text field)" - this change doesn't alter that
  contract, but the delta spec documents the dialog's sizing as responsive to terminal size
  (previously unspecified/implicitly fixed).

## Impact

- `src/lazynats/Values/CreateKeyDialog.cs`: `WrapField`'s hardcoded `Width = 43` and the Value
  frame's `Height = 10` become computed from `IApplication.Screen` (via
  `Services.Root.GetRequiredService<IApplication>()`, same as `ValueDetailDialog`).
- No changes to `CreateBucketDialog` or other dialogs - scoped to the key/value edit dialog only.
