## Why

The shortcut picker (`?`) currently lists entries as `Action (Key)` in a single, uncolored
string — the key is buried at the end of the line, in parentheses, after the thing it triggers.
Since the picker exists purely to be scanned quickly for "which key do I press," the key should
read first and stand out visually, matching the live feed's existing pattern of coloring the
part of a row that identifies it (subject, header, payload type).

## What Changes

- Reverse the shortcut picker's row order: key first, then action name (was: name, then key in
  parentheses).
- Color the key column green; the action name stays uncolored (the row's ambient/selected
  attribute).
- Right-pad the key column to a fixed width computed from the widest key actually present in the
  picker's current entry set (not a hardcoded guess) so no key text is ever truncated.
- Promote the live feed's internal `RowSegment`/`ColoredRow` colored-row-segment types (and the
  segment-walking render loop that draws them into a `ListView`) out of `LiveFeed/` into
  `Components/`, since the shortcut picker is now a second, non-feed consumer — reversing the
  explicit non-goal recorded in `live-feed-multicolor-rendering`'s design doc ("no public,
  reusable colored row renderer component for other views yet").

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `shortcut-picker`: entry presentation changes from `Action (Key)` (uncolored) to a colored,
  fixed-width key column followed by the uncolored action name.

## Impact

- `src/lazynats/Components/ShortcutPickerDialog.cs`: row text assembly and `ListView` rendering.
- `src/lazynats/LiveFeed/FeedRowFormatter.cs`, `LiveFeed/LiveLogDataSource.cs`: `RowSegment`/
  `ColoredRow` and the segment-walking render loop move to `Components/`; both files update their
  `using`s/call sites accordingly. No behavior change to the live feed itself.
- `src/lazynats/Theme.cs`: new color constant for the shortcut key column.
