## Why

Live feed rows currently color only the subject segment (`Theme.SubjectColor`); headers and the
payload's type prefix (`(json) `, `(text) `, `(blob) `) render in the row's plain color, making
them harder to pick out while skimming a fast-moving feed. Separately, an empty `Utf8Text`
payload renders as a bare `(text) ` prefix with nothing after it, which reads as a rendering
glitch rather than "this message had no body" — and when a message has no headers, the row still
reserves the same two-space gap around the (empty) header segment as when headers are present,
wasting horizontal width that's already scarce on a single-line row.

## What Changes

- Live feed rows render the header segment in a new lime-green theme color, and the payload's
  type prefix (`(json) `, `(text) `, `(blob) `) in a new white theme color — both alongside the
  existing subject coloring, not replacing it.
- An empty `Utf8Text` payload's single-line presentation renders as the literal string `(empty)`
  instead of `(text) ` followed by nothing. This only changes what text is produced for that one
  case; it does not add a new `PayloadContentKind`/`PayloadType` value.
- When a message has no headers, the live feed row omits the extra space that would otherwise
  separate the (empty) header segment from the subject and payload segments, instead of leaving a
  visible double-space gap.
- `FeedRow`'s single `Text` string plus `SubjectStart`/`SubjectLength` offset pair is replaced with
  a `ColoredRow` — an ordered list of `RowSegment`s (color + text), named generically because
  nothing about the shape or the render walk is feed-specific, even though today it lives in and
  is only used by the live feed. `LiveLogDataSource.Render` walks that list in one pass instead of
  the current copy-pasted single-span split logic, so a third (and any future) colored segment
  doesn't require hand-duplicating the viewport/width clamping arithmetic again.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `live-feed`: broadens the "Row Subject Text Is Colored" requirement to also color the header
  segment (lime green) and the payload's type prefix (white), and adds a requirement that the row
  text omits the extra separating space around headers when there are none.
- `payload-presentation`: the "Single-Line Presentation For Compact Display" requirement's
  `Utf8Text` case changes for an empty body — renders `(empty)` instead of `(text) `.

## Impact

- `src/lazynats/LiveFeed/FeedRowFormatter.cs` — builds the row as an ordered list of colored
  segments (subject, optionally headers, payload-type prefix, optionally payload body) instead of
  one concatenated string with offset math.
- `src/lazynats/LiveFeed/LiveLogDataSource.cs` — `Render` generalizes from one hardcoded span
  split to walking an ordered segment list with a running cursor.
- `src/lazynats/Payloads/PayloadPresentation.cs` — splits into a cheap `SingleLinePrefix` lookup
  (kind + emptiness → prefix literal, recomputed every render) and `RenderSingleLineBody` (the
  actual JSON/whitespace/hex work, still cached per envelope exactly as it is today).
- `src/lazynats/LiveFeed/FeedEnvelope.cs` — `CachedRowText` renamed `CachedPayloadBody`, now
  caching only the body text rather than prefix+body together.
- `src/lazynats/Theme.cs` — two new theme color constants (header color, payload-type-prefix
  color), following the existing `SubjectColor` pattern.
- No change to `PayloadType`/`PayloadContentKind` enums, to the Message Detail dialog, or to any
  other payload-presentation consumer (Publish/Templates) — this is scoped to the live feed row's
  compact single-line rendering.
