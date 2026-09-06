## Why

Every `Dialog<T>` subclass (`CreateStreamDialog`, `CreateConsumerDialog`, `CreateBucketDialog`,
`PatternDialog`, `HeaderDialog`) sets `Padding.Thickness = new Thickness(1, 0, 1, 0)`, giving
fields a left/right inset but zero rows of vertical breathing room above the first field — the
first label sits on the row directly under the title/border. The button row at the bottom already
reads as visually separated from the last field by a blank row (Dialog<T>'s own button-row
layout), so today every dialog looks bottom-heavy: airy below the fields, cramped above them. The
user wants dialogs to read as consistently sparse rather than lopsided, and wants this recorded as
a standing UI rule so future dialogs don't reintroduce the asymmetry one file at a time.

## What Changes

- Give every `Dialog<T>` subclass that has a button row (`CreateStreamDialog`,
  `CreateConsumerDialog`, `CreateBucketDialog`) one blank row between its title/border and its
  first field, by changing each one's `Padding.Thickness` top value from `0` to `1` (i.e.
  `new Thickness(1, 1, 1, 0)`). No field/label `Y` coordinates change — padding shifts the whole
  content area down, so the layout below the first row is unaffected.
- `PatternDialog`/`HeaderDialog` (single-field, no button row — Enter commits directly) are left
  as-is: they have no bottom blank row to balance against today, so they stay compact/horizontal
  rather than gaining unmatched top padding. This may be revisited later.
- Document a standing UI design rule (CLAUDE.md's "UI conventions" section, and the
  `dialog-spacing` spec) that a `Dialog<T>` subclass with a button row gives its content area one
  blank row above the first field/control, matching the blank row that already appears above the
  button row, so top and bottom read as symmetrically sparse.
- Purely spacing/rendering — no change to any dialog's fields, buttons, keybindings, validation,
  or commit/cancel logic.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dialog-spacing`: extend the existing modal-dialog spacing spec (currently horizontal
  title/body padding only) with a new requirement that `Dialog<T>` subclasses give their content
  area one blank row above the first field, matching the existing blank row above the button row.

## Impact

- `Streams/CreateStreamDialog.cs`, `Streams/CreateConsumerDialog.cs`,
  `KVStore/CreateBucketDialog.cs` — each one's `Padding.Thickness` line.
- `CLAUDE.md` — "UI conventions" section gains the new dialog-spacing rule.
- No new files; no NATS/Terminal.Gui dependency changes; no behavioral change.
