## Why

A live feed row is timestamp + subject + headers + payload run together as one plain-colored
line, so the subject — the field that identifies *what* a message is — doesn't stand out from
the rest of the row. `MessageDetailDialog` already colors the Subject field cyan for exactly this
reason ("Subject reads first and identifies the message"); the live feed list should get the same
treatment so a message's subject is scannable at a glance while skimming a fast-moving feed.

## What Changes

- Live feed rows render their subject segment in cyan (`ColorName16.Cyan`, matching
  `MessageDetailDialog`'s existing subject color), while the timestamp, headers, and payload
  segments keep their current, unchanged color.
- On a selected/highlighted row, the subject stays cyan on top of whatever background the row
  already has (normal or selected) — selection does not flatten it back to plain text.
- `FeedRowFormatter` exposes the subject's position (offset + length) within its formatted row
  text, alongside the existing full-text output, so `LiveLogDataSource.Render` can target just
  that span.
- `ColorName16.Cyan` is promoted from a literal duplicated at each call site to a single shared
  `Theme.cs` constant, read by both `MessageDetailDialog` and the new live feed row rendering.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: adds a requirement that a feed row's subject segment renders in the app's subject
  color, distinct from the rest of the row, including while the row is selected.

## Impact

- `src/lazynats/LiveFeed/FeedRowFormatter.cs`: return the subject's offset/length alongside the
  formatted row text.
- `src/lazynats/LiveFeed/LiveLogDataSource.cs`: draw the subject segment with a distinct
  attribute instead of one flat `AddStr`, composed with the existing horizontal-scroll
  (`viewportX`/`width`) slicing.
- `src/lazynats/Theme.cs`: add the shared subject-color constant.
- `src/lazynats/LiveFeed/MessageDetailDialog.cs`: read the color from `Theme.cs` instead of
  using `ColorName16.Cyan` inline.
