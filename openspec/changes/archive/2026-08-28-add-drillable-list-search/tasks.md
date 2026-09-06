## 1. `DrillableListView<T>` base: ordering and nearest-identity

- [x] 1.1 Sort items by `GetIdentity(item)` (`StringComparer.Ordinal`) whenever `ReplaceItems` sets
      the backing collection, so every subclass gets alphabetical order with no opt-in.
- [x] 1.2 Add a `NearestIdentity` lookup (binary-search insertion-point over the current sorted
      collection) alongside the existing `NeighborIdentity`, returning the nearest remaining
      identity by sort order, or `null` for an empty collection.
- [x] 1.3 Change `ReplaceItems`' fallback from "highlight the first item" to "highlight the item
      returned by `NearestIdentity` for the previously-highlighted identity."
- [x] 1.4 Update `drillable-list`'s existing consumers/tests (if any) that assumed "falls back to
      first item" — confirm no `*Tab.cs` logic depended on that specific fallback.

## 2. `DrillableListView<T>` base: quick-search shape

- [x] 2.1 Add an internal master/filtered-projection split: `ReplaceItems` populates the sorted
      master set; `PresenterListDataSource`/`ListView` render a filtered projection (equal to the
      master when the search field is empty).
- [x] 2.2 Implement the case-insensitive fuzzy-subsequence matcher (hand-rolled two-pointer scan
      over `GetIdentity(item)`, no library dependency) and use it to derive the filtered
      projection from the master set on every search-field keystroke.
- [x] 2.3 Add `Components/FilterBox.cs`: a standalone, persistent, always-visible search field
      (its own `EditFrame`-wrapped `TextField`, styled via `Theme.EditableBackground`) a Tab
      constructs and positions itself, as a sibling of the list's own `EditFrame` rather than
      nested inside it (nesting read as a frame within a frame - see design.md Decision 6).
- [x] 2.4 Add `DrillableListView<T>.AttachFilterBox(FilterBox box)`: links an externally-created
      box to the list via the narrow `IFilterable` interface (mirroring `EnableDescend`/
      `EnableAscend`/`EnableCreate`/`EnableDelete`/`EnableEdit`'s opt-in shape, but made by the
      owning Tab rather than the subclass itself, since only the Tab is positioned to place the
      box). Binds `/` to focus the box, and appends a "Search" hint to `Shortcuts` when attached.
- [x] 2.5 Wire the search field's `Esc` handling: clear text and return focus to the list when
      non-empty; when already empty, let `Esc` fall through to the list's own ascend binding
      (only meaningful where `EnableAscend` is also active).
- [x] 2.6 Reset the search field to empty whenever the item collection is replaced (`Ctrl+R` or any
      other refresh), and re-derive the filtered projection from the new master set.
- [x] 2.7 When a search keystroke removes the currently-highlighted item from the filtered
      projection, apply the same `NearestIdentity`-based fallback as `ReplaceItems` (task 1.3),
      scoped to the filtered projection.
- [x] 2.8 Ensure `SelectedItem`, `NeighborIdentity`, and any other existing `DrillableListView<T>`
      member that currently reads `_items` continues to operate over the filtered projection (i.e.
      "what's currently visible"), not the master set.

## 3. Wire a FilterBox into each list, from its owning Tab

- [x] 3.1 `Streams/StreamsTab.cs`: construct a `FilterBox` per level (stream/consumer), position
      each above its level's list `EditFrame`, call `AttachFilterBox` on the matching list, and
      toggle each box's `Visible` alongside its level's list frame in `Descend`/`Ascend`.
- [x] 3.2 `Values/ValuesTab.cs`: same, for the bucket/key levels.
- [x] 3.3 `Objects/ObjectsTab.cs`: same, for the bucket/object levels.
- [x] 3.4 For each Tab above, shift the list `EditFrame`'s `Y` down to `Pos.Bottom(filterBox)` (was
      directly below the level label) so the box and list frame stack as siblings, and `Add()` each
      list frame *before* its `FilterBox` — `ManagementTabs.FocusOwnContent()` resolves a tab's
      default focus via a depth-first walk of `Add()` order, so the reverse order would leave the
      search field, not the list, focused by default (see design.md Decision 6).

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` — confirm it compiles.
- [x] 4.2 Drive the app via `tmux` (per `CLAUDE.md`'s testing guidance) against a real NATS server:
      confirm alphabetical ordering, `/`-triggered search, fuzzy-subsequence matching (e.g. `oce`
      against a key named `OperationCancelledException`), Esc-clears-then-ascends behavior, and
      that a delete/refresh while filtered lands the highlight on a sensible neighbor — across at
      least the KV key list and one other list (e.g. streams).
- [x] 4.3 Confirm no existing shortcut hint or keybinding (`Ctrl+N/E/D/R`, `Esc`/`Backspace`) is
      broken by the new `/`-bound search field on any subclass.
