## Context

`FeedRowFormatter.Format(envelope)` returns one flat string (`timestamp  subject  headers
payload`) for a live feed row. `LiveLogDataSource.Render` (an `IListDataSource`, not a `View`
subclass) draws that whole string with a single `Move`/`AddStr` per row, after slicing it against
`viewportX`/`width` for horizontal scroll:

```csharp
var text = FeedRowFormatter.Format(_items[item]);
var visible = viewportX < text.Length ? text[viewportX..] : string.Empty;
if (visible.Length > width) visible = visible[..width];
listView.Move(col, row);
listView.AddStr(visible.PadRight(width));
```

No attribute is ever set here — the row is drawn using whatever attribute `ListView` already
committed to before calling `Render` (its own normal/selected/focused handling for that row).
`MessageDetailDialog.cs:111-115` already colors its read-only Subject field `ColorName16.Cyan`
on `Theme.EditableBackground` for the same "identifies the message" reason; this change gives the
live feed row the same visual cue, but has to do it mid-row instead of on a whole dedicated
control.

Confirmed via reflection against the referenced `Terminal.Gui` 2.4.10 build that `View.SetAttribute`,
`GetAttributeForRole`, and `SetAttributeForRole` are all `public`, so `LiveLogDataSource.Render`
can call `listView.SetAttribute(...)` directly even though it isn't a `View` subclass — the same
primitive `PollingDetailsView.OnDrawingContent` already uses internally for its label/value color
split.

## Goals / Non-Goals

**Goals:**
- Color only the subject segment of a live feed row cyan; timestamp, headers, and payload keep
  their current color.
- Preserve correct behavior under horizontal scroll (`viewportX`) and width clipping — the subject
  may be partially or fully scrolled out of view.
- Keep the cyan foreground on a selected/highlighted row, composed with whatever background
  `ListView` already applied for that row's selection state, rather than hardcoding a background.
- Share one color constant between `MessageDetailDialog` and the live feed instead of duplicating
  the `ColorName16.Cyan` literal.

**Non-Goals:**
- No general "rich segments" / markup capability for feed rows — this only needs one highlighted
  span (the subject), not an arbitrary list of colored ranges.
- No change to `FeedRowFormatter`'s payload caching (`CachedRowText`/`CachedContentKind`) —
  subject position is cheap arithmetic recomputed on every render, not cached.
- No change to `MessageDetailDialog`'s rendering mechanism (it keeps using `SetScheme` on its
  `TextView`), only the color source it reads from.

## Decisions

**Expose subject position, not a segments list.** `FeedRowFormatter` gains a second return
(subject start offset + length) alongside the existing full row text, computed in the same place
the text itself is built (fixed `HH:mm:ss.fff` timestamp prefix width + `message.Subject.Length`)
so the offset can never drift out of sync with a future change to the row's layout. Considered a
generic `(string Text, IReadOnlyList<(int Start, int Len, Color)> Spans)` shape instead — rejected
as unneeded generality for a single always-present highlighted field.

**Capture-and-restore the active attribute, don't hardcode a background.** `View.GetCurrentAttribute()`
peeks the attribute currently active without changing it. `Render` uses that to save the attribute
`ListView` already set for the row (normal or selected), swap in
`(Theme.SubjectColor, priorAttribute.Background)` for the subject span via `SetAttribute`, then
restore the prior attribute for the segments after it. This was the
resolved design question from exploration: cyan survives on top of selection highlighting instead
of `PollingDetailsView`'s pattern of hardcoding `GetAttributeForRole(VisualRole.Normal)`'s
background, which would only be correct for the unselected case.

**Three-segment draw composed with the existing scroll slicing.** `Render` keeps computing the
same visible-window slice of the full row text it does today (`[viewportX, viewportX +
visible.Length)`), then intersects that window with the subject's `[start, start+len)` range to
get up to three sub-slices (pre-subject, subject, post-subject) — any of which may be empty if the
subject is entirely scrolled out of view or the window falls entirely inside/outside it. Only the
subject sub-slice (if non-empty) gets the color swap; the others draw under the row's normal
attribute, unchanged from today.

**Shared `Theme.cs` constant.** Add `Theme.SubjectColor = new(ColorName16.Cyan)` alongside the
existing `LiveFeedFollowingColor`/`LiveFeedStickyColor` pattern; `MessageDetailDialog` reads from
it instead of its inline `ColorName16.Cyan` literal, so the two call sites can't drift apart.

## Risks / Trade-offs

- [Three-segment draw is more branchy than today's single `AddStr`] → Bounded to a handful of
  integer comparisons and at most 3 `AddStr` calls per visible row per redraw; no new allocation
  beyond the substrings already implied by the segments (same order of work as today's single
  slice-and-pad).
- [Subject offset derived from the timestamp format's fixed width could silently go stale if the
  row's prefix format changes] → Mitigated by keeping the offset computation inside
  `FeedRowFormatter` itself, next to the format string, rather than duplicated in
  `LiveLogDataSource`.

## Open Questions

None — the one open design question (cyan-on-selection vs. plain-on-selection) was resolved during
exploration in favor of cyan-on-selection.
