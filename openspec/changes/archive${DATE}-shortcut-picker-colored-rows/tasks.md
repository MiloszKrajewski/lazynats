## 1. Promote colored-row types and rendering to Components

- [x] 1.1 Move `RowSegment`/`ColoredRow` from `LiveFeed/FeedRowFormatter.cs` to a new
      `Components/ColoredRow.cs`, add the `Length` helper property, and update
      `FeedRowFormatter.cs`'s `using`/namespace references.
- [x] 1.2 Extract the segment-walking render loop from `LiveLogDataSource.Render` into
      `Components/ColoredRowRenderer.Render(ListView, ColoredRow, int col, int row, int width,
      int viewportX)`, moved verbatim (no logic change).
- [x] 1.3 Update `LiveLogDataSource.Render` to format the envelope and delegate to
      `ColoredRowRenderer.Render`.
- [x] 1.4 Add `Components/ColoredRowListDataSource.cs`: an `IListDataSource` over
      `ObservableCollection<ColoredRow>`, modeled on `PresenterListDataSource<T>`, delegating
      `Render` to `ColoredRowRenderer.Render`.

## 2. Recolor the shortcut picker

- [x] 2.1 Add `Theme.ShortcutKeyColor` (`ColorName16.BrightGreen`) to `Theme.cs`.
- [x] 2.2 In `ShortcutPickerDialog`, replace the `"{hint.Text} ({hint.Key})"` string rows with
      per-hint `ColoredRow`s: a `Theme.ShortcutKeyColor` key segment right-padded to the widest
      key chord in `sorted`, followed by a two-space-prefixed, uncolored name segment.
- [x] 2.3 Size the dialog's `ListView` width from `ColoredRow.Length` (replacing the old
      `rows.Max(row => row.Length)` over plain strings), keeping the existing `+4` padding and
      `Clamp(30, 60)` bounds.
- [x] 2.4 Wire the `ListView` to the new rows via
      `listView.Source = new ColoredRowListDataSource(new ObservableCollection<ColoredRow>(rows))`
      instead of `SetSource(new ObservableCollection<string>(rows))`.
- [x] 2.5 Update the `Result`-selection handler (`listView.Accepting`) to index back into the
      original `sorted` list by `SelectedItem`, unchanged in shape but confirm it still lines up
      one-to-one with the new `rows` list.

## 3. Verify

- [x] 3.1 Build the app (`dotnet build src/lazynats.sln`).
- [x] 3.2 **Adapted**: the `Ctrl+`-bound `ListEditorView` shortcuts this scenario names no longer
      exist as of `list-editor-plain-shared-shortcuts` (`grep WithCtrl` across `src` now returns
      nothing) — a design premise invalidated by a different change landing first, not by this
      one. Verified the equivalent (mixed key-length alignment) instead: via `tmux`, opened the
      picker from `SubscriptionsView` (single-letter keys `d`/`e`/`n`) and from a stream's
      consumer list, drilled in (`Esc` alongside `d`/`e`/`f`/`n`/`r`/`/`). `tmux capture-pane -e
      -p` confirmed via ANSI SGR codes: key text renders in `38;2;22;198;12` (BrightGreen), action
      names render uncolored, `Esc` (3 chars) renders in full un-truncated, and every row's name
      column starts at the same on-screen position regardless of its own key's length.
- [x] 3.3 Re-verified the live feed's existing subject/header/payload-type colors render unchanged
      after the `LiveLogDataSource`/`ColoredRowRenderer` extraction, per
      `doc/multi-color-rendering.md`'s ANSI-inspection recipe: published a message with a header
      via `nats pub` and confirmed via `tmux capture-pane -e -p` — subject in cyan (`0;255;255`),
      header in `50;205;50` (CSS LimeGreen), payload-type prefix in white (`255;255;255`),
      everything else uncolored, byte-identical to pre-change output.
- [x] 3.4 Confirmed via `tmux`: navigating to a non-default row and pressing Enter invokes that
      row's action (selected "Edit" → "Edit Subscription" dialog opened, not "New"'s dialog); Esc
      closes the picker with no action invoked, returning cleanly to the underlying view.
