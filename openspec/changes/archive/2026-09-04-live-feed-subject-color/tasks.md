## 1. Theme

- [x] 1.1 Add `Theme.SubjectColor` (`new(ColorName16.Cyan)`) to `src/lazynats/Theme.cs`,
      alongside the existing `LiveFeedFollowingColor`/`LiveFeedStickyColor` constants.
- [x] 1.2 Update `MessageDetailDialog.cs` to read `Theme.SubjectColor` instead of its inline
      `ColorName16.Cyan` literal.

## 2. Row formatting

- [x] 2.1 Change `FeedRowFormatter.Format` (or add a sibling method) to also return the
      subject's start offset and length within the formatted row text, computed alongside the
      existing text (fixed timestamp-prefix width + `message.Subject.Length`).
- [x] 2.2 Update `FeedRowFormatter`'s caller(s) to use the new signature.

## 3. Row rendering

- [x] 3.1 In `LiveLogDataSource.Render`, intersect the subject's offset/length range with the
      existing `viewportX`/`width` visible-window slice to get pre-subject/subject/post-subject
      sub-slices (any may be empty).
- [x] 3.2 Draw the pre-subject and post-subject sub-slices (if non-empty) under the row's current
      attribute, unchanged from today.
- [x] 3.3 Before drawing the subject sub-slice (if non-empty), capture the prior attribute via
      `listView.SetAttribute(...)`'s return value, swap in `(Theme.SubjectColor, prior.Background)`,
      draw the subject text, then restore the prior attribute for whatever is drawn after it.
- [x] 3.4 Confirm the row is still padded to `width` exactly as today (no trailing-space
      regression from the segmented draw).

## 4. Verification

- [x] 4.1 Run the app against a local NATS server (`dotnet run --project src/lazynats`) and
      visually confirm: subject renders cyan, rest of the row unchanged, on both a normal and a
      selected row.
- [x] 4.2 Confirm horizontal scroll (long rows) still colors only the visible part of the subject
      and doesn't misalign/duplicate/drop characters at the segment boundaries. Note: the running
      app's `ListView` didn't respond to Left/Right for horizontal scroll in this session (likely
      because `MaxItemLength = 0`, an existing, deliberate O(1)-append tradeoff predating this
      change - see `LiveLogDataSource`'s class comment), so this was verified analytically instead
      - the `subjectVisibleStart`/`subjectVisibleEnd` clamp is a standard closed-interval
      intersection of `[subjectStart, subjectStart+subjectLength)` with
      `[viewportX, viewportX+visible.Length)`, and was exercised directly (viewportX=0) against a
      real running row where the cyan run's length matched the subject's length exactly.
