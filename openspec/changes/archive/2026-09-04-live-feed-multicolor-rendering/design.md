## Context

`doc/multi-color-rendering.md` documents two idioms for coloring part of a Terminal.Gui line;
the live feed row currently uses idiom 2 (external renderer, capture-and-restore) for exactly one
span — the subject, via `FeedRow.SubjectStart/SubjectLength` (offsets into a single pre-assembled
string) and a single hand-written before/span/after split in `LiveLogDataSource.Render`. This
change adds two more colored regions (headers, payload-type prefix) to the same row. Extending the
offset-pair approach a second and third time would mean `FeedRowFormatter` hand-computing three
sets of `Start`/`Length` values against one concatenated string (`subjectStart = timestampText
.Length + 2`-style arithmetic, repeated and compounding), and `LiveLogDataSource.Render`
re-deriving the same viewport-clamp arithmetic three times. The design below replaces that with a
row represented directly as an ordered sequence of colored text segments, which removes the offset
bookkeeping entirely and lets the render loop walk it generically regardless of how many segments
a row has.

## Goals / Non-Goals

**Goals:**
- Color the header segment (lime green) and the payload's type prefix (white), composing with the
  existing subject coloring and with row selection, exactly as the subject already does.
- Replace `FeedRow`'s single `Text` string + `SubjectStart`/`SubjectLength` offset pair with a
  `ColoredRow` — an ordered `IReadOnlyList<RowSegment>`, each segment a `(Color? Color, string
  Text)` pair, `Color: null` meaning "leave the ambient attribute alone." Both types are named
  generically, not `FeedRow`/`FeedRowSegment`, because neither the shape nor the render walk
  depends on anything feed-specific — only `FeedRowFormatter` (the assembly logic) and
  `LiveLogDataSource` (the `FeedEnvelope` binding) actually are. `FeedRowFormatter` builds the row
  by appending segments in order (no index arithmetic) and `LiveLogDataSource.Render` walks that
  list with one small, segment-count-agnostic loop.
- Render an empty `Utf8Text` payload as `(empty)` instead of `(text) ` followed by nothing.
- Drop the redundant space pair around an empty header segment.
- Keep the cached, potentially-expensive part of payload rendering scoped to exactly what it is
  today: the decoded/minified/hex-encoded *body*. The type prefix (`(json) `, `(text) `, `(blob) `,
  or `(empty)`) is a fixed short literal selected by a plain `switch` on the already-cached
  `PayloadContentKind` — cheap enough to recompute on every render, so it must not gate or expand
  what gets cached on `FeedEnvelope`.

**Non-Goals:**
- No change to `PayloadType`/`PayloadContentKind` — `(empty)` is a display string for the existing
  `Utf8Text` classification when the rendered body is zero-length, not a new enum value.
- No change to how the Message Detail dialog, Publish, or Templates render payloads — this is
  scoped to the live feed row's single-line presentation and `LiveLogDataSource`.
- No public, reusable "colored row renderer" component for other views yet, and no relocation out
  of `LiveFeed/` into `Components/` (the codebase's home for things actually shared across tabs
  today, per `CLAUDE.md`) — despite `RowSegment`/`ColoredRow` being generically named and the
  segment-walking loop having no feed-specific dependency, moving them now would be designing for a
  hypothetical second consumer that doesn't exist yet (YAGNI). Generic naming costs nothing today;
  physically relocating code does, so it waits for an actual second consumer.

## Decisions

**`FeedRow` becomes `ColoredRow`, an ordered list of colored segments, not a string plus
offsets — named generically because nothing about the shape or the render walk is feed-specific.**

```csharp
internal readonly record struct RowSegment(Color? Color, string Text);
internal readonly record struct ColoredRow(IReadOnlyList<RowSegment> Segments);
```

`FeedRowFormatter.Format` builds `Segments` by appending each piece as it's produced — there is no
separate string to assemble first and no `Start`/`Length` to compute and keep in sync with it:

```csharp
var segments = new List<RowSegment>(6) {
    new(null, $"{timestampText}  "),
    new(Theme.SubjectColor, message.Subject),
};
if (headerText.Length > 0) {
    segments.Add(new(null, "  "));
    segments.Add(new(Theme.HeaderColor, headerText));
}
segments.Add(new(null, "  "));
segments.Add(new(Theme.PayloadTypeColor, prefix));
if (body.Length > 0) segments.Add(new(null, body));
```

The `headerText.Length > 0` / `body.Length > 0` guards are also how the "no redundant header
spacing" and "empty payload has no separate body segment" requirements fall out naturally, rather
than needing a special case bolted on afterward.

**`LiveLogDataSource.Render` walks segments with a running cursor, not precomputed offsets.**
Segments are inherently ordered and contiguous (each one picks up exactly where the previous left
off), so the render loop tracks a single `cursor` (position in the row's full, unclipped text),
and for each segment: compute `[cursor, cursor + segment.Text.Length)`, intersect with the visible
window `[viewportX, viewportX + width)` (same closed-interval clamp `doc/multi-color-rendering.md`
already documents — `Math.Max(...)`/`Math.Min(...)`, skip when the intersection is empty), slice
out only that intersected portion of `segment.Text`, and draw it — under the segment's own color
(capture-and-restore via `GetCurrentAttribute()`/`SetAttribute`, idiom 2) when `Color` is set, or
under whatever attribute is already active when it's `null`. After the loop, pad any remaining
width with spaces, same as today. This is the same clamp arithmetic the current single-span
version already uses, just applied once per segment in a loop instead of hand-written three times
— and it incidentally avoids the current code's upfront `text[viewportX..]` substring (which can
allocate more than `width` characters before being re-sliced): each segment is sliced to exactly
its visible portion and no more.

**`Color?` (nullable), not a `Theme.Normal` sentinel, represents "don't override."** A literal
"normal" color would fight row selection — `doc/multi-color-rendering.md` calls out exactly this
("hardcoding `VisualRole.Normal`'s background would look wrong on a selected row"). `null` means
the segment is drawn under whatever attribute the row already has (selected, focused, or plain),
identical to how today's before/after text around the subject span is never touched.

**Payload rendering splits into a cheap prefix lookup and the one expensive, cached body
render — the cache stays scoped to the body, unchanged from today.** `PayloadPresentation` gains:

```csharp
public static string SingleLinePrefix(PayloadContentKind kind, bool bodyEmpty) => kind switch {
    PayloadContentKind.Json => "(json) ",
    PayloadContentKind.Utf8Text => bodyEmpty ? "(empty)" : "(text) ",
    PayloadContentKind.Binary => "(blob) ",
    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
};

public static string RenderSingleLineBody(byte[] data, PayloadContentKind kind, int maxLength) => kind switch {
    PayloadContentKind.Json => Cap(RenderMinifiedJson(data), maxLength),
    PayloadContentKind.Utf8Text => Cap(CollapseWhitespace(Encoding.UTF8.GetString(data)), maxLength),
    PayloadContentKind.Binary => RenderHexBudgeted(data, maxLength),
    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
};
```

`RenderSingleLineBody` is exactly today's `RenderSingleLine` minus the prefix string literals —
same JSON-parse/minify, whitespace-collapse, and byte-budgeted hex work, so it's exactly as
expensive as today and exactly what stays cached. `SingleLinePrefix` is a plain `switch` over an
already-known `PayloadContentKind` and a bool — there is nothing in it worth caching, so it isn't:
`FeedRowFormatter` calls it fresh on every render, passing `bodyEmpty` from the *cached* body's own
`Length == 0` check.

`FeedEnvelope.CachedRowText` (a `string?`) is renamed `CachedPayloadBody` and now holds only the
body — the same value it effectively holds today, just without the prefix concatenated onto it:

```csharp
var kind = envelope.CachedContentKind ??= PayloadContentProbe.Classify(data);
var body = envelope.CachedPayloadBody ??= PayloadPresentation.RenderSingleLineBody(data, kind, MaxPayloadBodyLength);
var prefix = PayloadPresentation.SingleLinePrefix(kind, body.Length == 0);
```

## Risks / Trade-offs

- **Segment-list construction happens on every render (not cached), unlike the old single flat
  string** → this is intentional and cheap: it's a handful of list appends over already-computed
  or already-cached strings (timestamp formatting, the cached payload body, `Theme` color lookups),
  not re-doing any of the actual expensive work. No different in cost from today's row-text
  interpolation, which was also rebuilt fresh every render.
- **Changing `PayloadPresentation`'s public surface (`RenderSingleLine` → `SingleLinePrefix` +
  `RenderSingleLineBody`) is a signature break** → contained: the only call site is
  `FeedRowFormatter`; grep confirms no other consumer depends on the old shape.
- **A segment walk with an off-by-one in the cursor/clamp math would misrender every row, not just
  the new segments** → mitigated by keeping the per-segment clamp identical to the existing,
  already-verified single-span version (same `Math.Max`/`Math.Min` shape), and by re-running the
  doc's `tmux capture-pane -e` ANSI-inspection recipe against a selected row for all segments, not
  just the new ones, before considering this done.

## Migration Plan

Pure rendering change, no persisted state or external interface involved — no migration or
rollback steps beyond the normal build/verify/commit cycle.
