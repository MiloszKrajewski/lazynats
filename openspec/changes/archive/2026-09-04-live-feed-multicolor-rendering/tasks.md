## 1. Theme colors

- [x] 1.1 Add `Theme.HeaderColor` (`new Color(50, 205, 50)`, CSS `LimeGreen`) next to
  `Theme.SubjectColor`, with a doc-comment noting what it's shared with (mirrors the existing
  `SubjectColor` comment style).
- [x] 1.2 Add `Theme.PayloadTypeColor` (`ColorName16.White`) next to `Theme.SubjectColor`.

## 2. Payload presentation: split prefix from body

- [x] 2.1 Add `PayloadPresentation.SingleLinePrefix(PayloadContentKind kind, bool bodyEmpty) ->
  string`: `Json => "(json) "`, `Utf8Text => bodyEmpty ? "(empty)" : "(text) "`,
  `Binary => "(blob) "`.
- [x] 2.2 Rename `RenderSingleLine` to `RenderSingleLineBody`, dropping the prefix string literals
  it currently concatenates — same JSON-minify/whitespace-collapse/hex-budgeting logic, body only.
- [x] 2.3 Confirm `CollapseWhitespace` on an empty/all-whitespace `Utf8Text` payload naturally
  yields an empty string (no separate empty-check needed inside `RenderSingleLineBody` itself —
  emptiness is just `body.Length == 0`, decided by the caller).

## 3. FeedEnvelope cache + FeedRowFormatter segments

- [x] 3.1 Rename `FeedEnvelope.CachedRowText` to `CachedPayloadBody` (still `string?`) — it now
  caches only the body, not prefix+body.
- [x] 3.2 Add `RowSegment(Color? Color, string Text)` readonly record struct — named generically
  (not `FeedRowSegment`), since nothing about it is feed-specific.
- [x] 3.3 Rename `FeedRow` to `ColoredRow` and change it from `(string Text, int SubjectStart, int
  SubjectLength)` to `(IReadOnlyList<RowSegment> Segments)` — same reasoning, generic name for a
  generic shape.
- [x] 3.4 In `FeedRowFormatter`, replace `RenderPayload` with logic that: gets/caches `kind` via
  `CachedContentKind` (unchanged), gets/caches `body` via `CachedPayloadBody ??=
  RenderSingleLineBody(...)`, then computes `prefix` fresh via `SingleLinePrefix(kind, body.Length
  == 0)`.
- [x] 3.5 In `FeedRowFormatter.Format`, build `Segments` by appending in order: `(null,
  "{timestamp}  ")`, `(Theme.SubjectColor, subject)`; then, only if headers are non-empty, `(null,
  "  ")` + `(Theme.HeaderColor, headerText)`; then `(null, "  ")`, `(Theme.PayloadTypeColor,
  prefix)`; then, only if `body.Length > 0`, `(null, body)`.

## 4. LiveLogDataSource: segment-walk render

- [x] 4.1 Replace `Render`'s single hardcoded subject-span split with a loop over
  `ColoredRow.Segments`, tracking a running `cursor`: for each segment, compute
  `[cursor, cursor + segment.Text.Length)`, intersect with `[viewportX, viewportX + width)`, skip
  if the intersection is empty, otherwise slice `segment.Text` to that intersection and draw it —
  under the segment's `Color` (capture-and-restore via `GetCurrentAttribute()`/`SetAttribute`, per
  `doc/multi-color-rendering.md` idiom 2) when non-null, or under the current attribute unchanged
  when `Color` is `null` — then advance `cursor`.
- [x] 4.2 After the loop, pad any remaining width with spaces (track total visible characters
  written, pad `width - written`), matching today's trailing `PadRight(width)` behavior.
- [x] 4.3 Confirm the loop produces the same on-screen output as the current implementation for
  the pre-existing subject-only-span case (no headers, no payload-prefix coloring) before adding
  the new segment kinds — i.e. refactor first, verify no regression, then extend.

## 5. Verification

- [x] 5.1 Build (`dotnet build src/lazynats.sln`) and fix any compile errors from the
  `FeedRow`→`ColoredRow`/`PayloadPresentation` signature changes at their call sites.
- [x] 5.2 Launch via `tmux` against a running local NATS server; publish messages covering: headers
  present, no headers, non-empty text payload, empty text payload, JSON payload, binary payload.
- [x] 5.3 Use `tmux capture-pane -e -p` + the doc's ANSI-run inspection script to confirm: header
  segment is lime green, payload-type prefix is white, subject coloring is unchanged, and colors
  compose correctly on a selected row.
- [x] 5.4 Visually confirm (via `tmux capture-pane -p`) that a no-header row has no double-space
  gap before the payload, and that a with-headers row's spacing is unchanged from before this
  change.
- [x] 5.5 Confirm an empty-payload message's row shows `(empty)` (not `(text) ` followed by
  nothing), colored white like every other payload-type prefix.

## 6. OpenSpec sync

- [x] 6.1 Run `openspec archive` (or the `opsx:archive` skill) once implementation and manual
  verification above are complete, syncing the `live-feed` and `payload-presentation` delta specs
  into `openspec/specs/`.
