## Context

`ShortcutPickerDialog` currently builds each row as a plain string, `"{hint.Text} ({hint.Key})"`,
and hands a `List<string>` to `ListView.SetSource` — Terminal.Gui's default `ListWrapper<string>`
draws it verbatim, no per-segment color possible.

The live feed already solved "color part of a `ListView` row" for exactly this reason:
`RowSegment`/`ColoredRow` (an ordered list of `(Color?, string)` segments) plus a segment-walking
render loop in `LiveLogDataSource.Render` that intersects each segment with the viewport and
draws it via capture-and-restore (`doc/multi-color-rendering.md` idiom 2). That change's design
doc explicitly deferred generalizing this ("no public, reusable colored row renderer component
for other views yet... moving them now would be designing for a hypothetical second consumer that
doesn't exist yet"). This change *is* that second consumer, so the deferral no longer applies.

Checked what `Key.ToString()` (Terminal.Gui 2.4.17) actually produces for the picker's current
entries (everything advertised via `IShortcutSource`, i.e. excluding the hardcoded top-level
`Alt-N`/`Alt-P`/`?` shortcuts already filtered out today): `Esc`, `/`, single letters (`R`, `N`,
`D`, `E`, `F`, `S`, `X`, `O`, `V`), and `Ctrl+N`/`Ctrl+E`/`Ctrl+D`/`Ctrl+F` from
`ListEditorView`'s bound-mode shortcuts. The widest of these today is 6 characters (`Ctrl+N`),
not 5 (`Space`, which doesn't currently appear at all) — confirming the key column width must be
computed from the live entry set each time the picker opens, not hardcoded, or a future/less
common longer key (or a currently-existing `Ctrl+`-prefixed one) would get truncated.

## Goals / Non-Goals

**Goals:**
- Shortcut picker rows read as `<key, padded, green><two spaces><action name, uncolored>` —
  reversed from today's `<name> (<key>)`.
- Key column width is computed once per dialog open, from the longest key text actually present
  in that open's (already-filtered, already-sorted) entry set — never a hardcoded guess, so no
  key is ever truncated regardless of what shortcuts happen to be advertised.
- Promote `RowSegment`/`ColoredRow` from `LiveFeed/` to `Components/` (generic already, per their
  original naming — only their location changes), plus the segment-walking render loop
  (extracted from `LiveLogDataSource.Render` into a shared `ColoredRowRenderer.Render` helper) so
  both the live feed and the shortcut picker draw colored rows through the same code path.
- Add a small, generic `IListDataSource` (`ColoredRowListDataSource`) for a static/bound
  `ObservableCollection<ColoredRow>`, following the existing `PresenterListDataSource<T>` shape,
  so `ShortcutPickerDialog` can hand `ListView` pre-built colored rows the same way
  `LiveLogDataSource` does today.

**Non-Goals:**
- No change to what shortcuts are listed, their sort order, or the empty-state/selection/Enter/Esc
  behavior already specified in `shortcut-picker` — only how each row is drawn.
- No change to the live feed's rendered output — `FeedRowFormatter`/`LiveLogDataSource` keep
  producing byte-identical `ColoredRow`s, just built from types now imported from `Components`
  instead of declared locally.
- No general-purpose "table" or multi-column list component — the key/name split here is just two
  `RowSegment`s, not a new abstraction beyond what `ColoredRow` already provides.

## Decisions

**`RowSegment`/`ColoredRow` move to `Components/ColoredRow.cs` verbatim; only their namespace
changes.** They were already named generically (not `FeedRow`/`FeedRowSegment`) in anticipation
of exactly this. Add one small helper alongside them:

```csharp
internal readonly record struct ColoredRow(IReadOnlyList<RowSegment> Segments)
{
    public int Length => Segments.Sum(s => s.Text.Length);
}
```

`Length` replaces the picker's current `rows.Max(row => row.Length)` (which operated on plain
strings) and is generically useful for any future colored-row consumer sizing a container to fit.

**The segment-walking render loop moves out of `LiveLogDataSource.Render` into
`Components/ColoredRowRenderer.Render(ListView, ColoredRow, int col, int row, int width, int
viewportX)`.** It's already segment-count-agnostic and has zero `FeedEnvelope`-specific logic
(cursor tracking, viewport intersection, capture-and-restore color, pad-to-width) — extracting it
verbatim avoids a second, drifting copy in a new `ColoredRowListDataSource`.
`LiveLogDataSource.Render` becomes a one-liner: format the envelope, then delegate.

**New `Components/ColoredRowListDataSource.cs`, modeled on `PresenterListDataSource<T>` (same
file, adjacent responsibility) rather than on `LiveLogDataSource`.** Takes an
`ObservableCollection<ColoredRow>` (matching `ListView.SetSource`'s existing
`ObservableCollection<string>` idiom the picker already uses, just with the element type changed)
and delegates `Render` to `ColoredRowRenderer.Render`. `MaxItemLength = 0`, same rationale as
`PresenterListDataSource`/`LiveLogDataSource` (avoid any rescan cost) — the picker already sizes
its `ListView` to fit the widest row exactly, so horizontal scroll never engages either way.

**`ShortcutPickerDialog` builds one `ColoredRow` per hint: a green, right-padded key segment
followed by a two-space-prefixed, uncolored name segment.**

```csharp
var keyTexts = sorted.Select(hint => hint.Key.ToString()).ToList();
var keyColumnWidth = keyTexts.Max(text => text.Length);
var rows = sorted.Zip(keyTexts, (hint, keyText) => new ColoredRow([
    new RowSegment(Theme.ShortcutKeyColor, keyText.PadRight(keyColumnWidth)),
    new RowSegment(null, $"  {hint.Text}"),
])).ToList();
var width = Math.Clamp(rows.Max(row => row.Length) + 4, 30, 60);
...
listView.Source = new ColoredRowListDataSource(new ObservableCollection<ColoredRow>(rows));
```

`keyColumnWidth` is recomputed fresh every time the dialog is constructed (i.e. every picker
open) from whatever hints the current focus chain advertises that time — cheap (a handful of
short strings) and correct regardless of which view was focused when `?` was pressed.

**New `Theme.ShortcutKeyColor` (`ColorName16.BrightGreen`), not a reused existing green.**
`Theme.HeaderColor` (CSS LimeGreen) already means "this is a message header" in the live feed;
reusing it for "this is a keyboard shortcut" would conflate two unrelated meanings that happen to
share a hue. `Theme.cs`'s own pattern is one named constant per semantic use (`SubjectColor`,
`HeaderColor`, `PayloadTypeColor` are all distinct despite some being visually similar), so this
follows suit. `BrightGreen` (not `HeaderColor`'s LimeGreen RGB) both reads as unambiguously
"green" on a 16-color terminal fallback and stays visually distinct from `HeaderColor` on a
truecolor one.

## Risks / Trade-offs

- **A dynamically-computed key column width means the picker's visual alignment shifts slightly
  depending on which view is focused when it's opened** (e.g. 6-wide when a `ListEditorView`-bound
  view is focused, narrower elsewhere) → acceptable: the picker is a transient, single-view-scoped
  dialog re-opened fresh each time, not a persistent layout users compare across opens.
- **Extracting `ColoredRowRenderer` out of `LiveLogDataSource` touches the live feed's render path**
  → mitigated by moving the loop body verbatim (no logic change) and re-verifying the live feed's
  existing colored segments (subject/header/payload-type) render identically afterward, per
  `doc/multi-color-rendering.md`'s `tmux capture-pane -e` ANSI-inspection recipe.

## Migration Plan

Pure rendering/refactor change, no persisted state. Verify via `tmux`: open the picker from a
view with `Ctrl+`-bound shortcuts (e.g. a `ListEditorView`-backed tab) and confirm the key column
isn't truncated and lines up across rows; re-run the live feed's existing color verification to
confirm the `LiveLogDataSource` extraction didn't change its output.
