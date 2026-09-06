## Context

`DrillableListView<T>` (`src/lazynats/Components/DrillableListView.cs`) is the shared base behind
six lists today: `StreamListView`, `ConsumerListView`, the KV and OBJ `BucketListView`s,
`KeyListView`, and `ObjectListView`. Every subclass already supplies `GetIdentity(T item)` (used
for `ReplaceItems`' highlight-preservation and `NeighborIdentity`'s post-delete lookup) and a
presenter for row formatting. Items are currently displayed in whatever order the owning tab's
fetch produced (server/arrival order), and `ReplaceItems` falls back to highlighting the first item
whenever the previously-highlighted identity is no longer present. Every `*Tab.cs` fetch/refresh
method is unaffected by this change — this is a presentation-layer change only, over whatever is
already loaded into memory.

## Goals / Non-Goals

**Goals:**
- Every drillable list displays its items in ascending alphabetical order by identity, by default,
  with no per-subclass opt-in required.
- An opt-in quick-search shape, matching the base's existing `EnableDescend`/`EnableAscend`/
  `EnableCreate`/`EnableDelete`/`EnableEdit` pattern, adds a persistent search field that filters
  the currently-loaded items live, in memory, via case-insensitive fuzzy-subsequence matching
  (`oce` ~ `*o*c*e*`).
- Losing the current highlight — whether from a quick-search keystroke narrowing the view, or from
  any other cause `ReplaceItems` already handles (refresh, post-delete) — lands on the nearest
  remaining item in sort order, not the top of the list.

**Non-Goals:**
- No change to what any tab fetches from the server, or when. KV's very-large-bucket problem (a
  pre-fetch NATS-pattern filter, a fetch cap, a truncation indicator) is a separate, deferred
  change.
- No relevance-scored/ranked fuzzy matching (fzf-style reordering). Matching here is a pure
  predicate; a filtered result set keeps the same alphabetical order as the unfiltered list.
- No persistence of quick-search text across navigation (ascend/descend) or across app sessions.

## Decisions

**1. Sorting is base-default, not opt-in, and needs no new abstract member.** Every subclass
already implements `GetIdentity(T) -> string`. `ReplaceItems` sorts incoming items by
`GetIdentity(item)` using `StringComparer.Ordinal` before they reach the list, so all six existing
subclasses get alphabetical order automatically, with zero subclass-side changes. Considered
adding a new `IComparer<T>`/`SortKey` abstract member for more flexibility; rejected as
unnecessary — nothing about these lists needs anything other than identity order, and reusing
`GetIdentity` keeps the surface area small.

**2. Quick-search is a filtered projection over a retained master collection, not a destructive
in-place filter.** `ReplaceItems` continues to populate a full, sorted master set exactly as today;
a new internal filtered projection (identical to the master when the search field is empty) is
what actually backs `PresenterListDataSource`/`ListView`. `SelectedItem`, `NeighborIdentity`, and
everything downstream (`KeyListView.SelectedKey`, delete/edit flows in `ValuesTab`/`ObjectsTab`/
`StreamsTab`) continue to operate over "whatever is currently visible" — i.e. the filtered
projection — so none of their existing call sites or contracts change meaning. Typing in the
search field re-derives the filtered projection from the master; it never triggers a
`RefreshRequested`/re-fetch.

**3. A new `NearestIdentity` lookup replaces "first item" as `ReplaceItems`' fallback, and also
backs the quick-search case.** Given a target identity no longer present in the current (sorted)
collection, `NearestIdentity` does a binary-search-style insertion-point lookup and returns
whichever neighboring identity is actually present — the same shape as the existing
`NeighborIdentity`, but by sort order rather than list-index adjacency, and usable on either the
master or the filtered projection since both are sorted. `ReplaceItems`' existing "falls back to
the first item" behavior (see `drillable-list`'s "Identity-Preserving Replace" requirement) is
superseded by this for every subclass — a nearest neighbor by sort order is a strict improvement
over "jump to the top of a possibly-huge list" and keeping two different fallback mechanisms side
by side (index-based here, sort-based there) would be its own inconsistency. This also covers the
quick-search case for free: when a keystroke removes the highlighted item from the filtered
projection, that's the same "highlighted identity no longer present" situation `ReplaceItems`
already handles.

**4. Fuzzy-subsequence matching runs against `GetIdentity(item)`, not the presenter's rendered row
text.** Considered matching against whatever `IValuePresenter<T>` renders (closer to "search what
you see"), but identity is already the canonical string this base uses everywhere else (highlight
preservation, neighbor lookup), some presenters render more than bare identity (e.g. bucket stats),
and for the two subclasses this matters most for today (`KeyListView`, `ObjectListView`) identity
*is* the full displayed text anyway. Hand-rolled two-pointer scan, no library dependency — stays
`PublishAot`-friendly and the algorithm is a few lines (advance a query pointer through the target
string, case-insensitively, matching each query character in order; match succeeds iff the query
pointer reaches the end).

**5. Search field keybinding: `/` opens/focuses it, matching `less`/`vim`/`lazygit`'s own search
convention** — deliberately not `Ctrl+F`, which earlier discussion of the (separate, deferred) KV
pre-fetch-pattern change reserved for that dialog, to avoid the two changes colliding on the same
key later. `Esc` inside the field clears its text and returns focus to the list if it has text;
if it's already empty, `Esc` falls through to whatever ascend behavior the list already has (so
search doesn't swallow existing back-navigation). Search text resets to empty on ascend/descend and
on `Ctrl+R` refresh — no persistence, consistent with the decision already made for KV's (deferred)
per-bucket pattern.

**6. The search field is a standalone `FilterBox` View a Tab constructs and positions itself, not
a child `DrillableListView<T>` creates and embeds in its own layout.** First cut embedded the field
(wrapped in its own `EditFrame`) directly inside `DrillableListView<T>`, above the inner `ListView`
— but since the whole component is already wrapped in an `EditFrame` by its owning Tab (e.g.
`ValuesTab._bucketListFrame`), that nested a bordered edit control inside another bordered edit
control, reading as a frame within a frame (doubled borders/shadow). Revised shape: `FilterBox`
(`Components/FilterBox.cs`) is a small standalone `View` — its own single `EditFrame`-wrapped
`TextField`, nothing else — that a Tab creates and positions as a sibling of the list's `EditFrame`
(above it, not inside it; see each `*Tab.cs`'s layout). `DrillableListView<T>.AttachFilterBox(box)`
links the two: it stores the box and calls `box.AttachTo(this)`, where `this` is exposed to the box
only through a narrow `IFilterable` interface (`ApplyFilter`/`FocusList`/`HandleEmptySearchEscape`)
— the box never depends on `DrillableListView<T>` directly, and the list never constructs or owns a
`TextField` itself. `EnableSearch()` (a same-shaped opt-in called from the subclass's own
constructor, per the `EnableDescend`/`EnableAscend`/... pattern) is dropped entirely: since the box
lives outside the list's own View subtree, only the owning Tab is positioned to decide whether and
where to place one, so attaching *is* the opt-in.

**7. Keyboard navigation around a `FilterBox` is handled explicitly, point-to-point, rather than
via Terminal.Gui's generic TabStop/AdvanceFocus machinery.** Adding a second focusable View (the
box) alongside each list surfaced several Terminal.Gui default-navigation gaps a single-focusable-
child tab never hit, and - contrary to the framework's own documented model - none of them were
fixable by *configuring* that machinery (`TabBehavior.TabGroup`, explicit `KeyBindings`) from
`FilterBox`/`DrillableListView<T>` themselves:
  - *Tab/Shift+Tab order should match spatial layout.* Each `*Tab.cs` `Add()`s a level's
    `FilterBox` before its list `EditFrame` (matching the box sitting visually above the list).
  - *That breaks default focus-on-entry, so it's fixed independently.* Both ways a tab can become
    active - `ManagementTabs.FocusOwnContent()` (Down-arrow from a focused header) and
    `ManagementTabs.SelectTab(tab)` (used by every `Alt+1..4` shortcut and the initial tab, in
    place of assigning `Value` directly, which resolves focus via a *different*, FilterBox-unaware
    default) - resolve the default focus target via `FindFirstFocusableDescendant`, which
    explicitly skips any `FilterBox` it walks past. This keeps "Tab-key order is spatial" and
    "default focus is always the list" independently true.
  - *Up-arrow at the top of the list, and Tab/Shift+Tab/Down from the FilterBox, are wired
    point-to-point rather than left to escape via Terminal.Gui's own boundary handling.* At a
    list's top, unhandled `Command.Up` otherwise bubbles past the box straight to the tab's own
    header (`DrillableListView<T>.AttachFilterBox` binds `Command.Up` directly to
    `box.Focus()`). `FilterBox` binds `Command.Down` directly to `IFilterable.FocusList()`
    (`_target.FocusList()`).
  - *Tab/Shift+Tab specifically can only be intercepted from `ManagementTabs`, not from `FilterBox`
    or `DrillableListView<T>`.* Confirmed empirically: neither a `KeyDown` handler nor an
    `AddCommand`/`KeyBindings` pair on `FilterBox` itself ever sees a Tab/Shift+Tab keypress -
    Terminal.Gui appears to route them straight to the nearest enclosing `TabBehavior.TabGroup`
    (every management tab's content is forced to plain `TabStop` by `Tabs.OnSubViewAdded`
    regardless of what's set beforehand, and `ManagementTabs`/`Tabs` itself is the `TabGroup`),
    bypassing the normal per-view KeyBindings bubble-up entirely. So `ManagementTabs` binds
    `Key.Tab`/`Key.Tab.WithShift` on itself and, in `AdvanceWithinPage`, walks up from the actually-
    focused view (`App.Navigation.GetFocused()`, not `Value`'s immediate child) looking for a
    `FilterBox` or an `IFilterable` (`DrillableListView<T>`) and focuses the other half of that
    pairing directly - never calling Terminal.Gui's generic `AdvanceFocus` for this case at all
    (falling back to it only when focus isn't within any such pairing). Deliberately bound to
    `Command.Accept`, not `Command.NextTabStop`/`PreviousTabStop`: reproduced repeatedly that
    routing through those two specific commands - even with a custom handler replacing their
    built-in behavior - left Terminal.Gui's own internal Tab-navigation bookkeeping in a state
    where, once `PreviousTabStop` (Shift+Tab) had fired once in a session, `NextTabStop` (Tab)
    silently stopped reaching `ManagementTabs`' KeyBindings at all for the rest of the session
    (Shift+Tab kept working indefinitely; only Tab broke, and only after the first Shift+Tab). The
    toggle itself is direction-agnostic anyway (there are only ever the two stops), so both keys
    are bound to the one arbitrarily-chosen, otherwise-unused `Command.Accept` slot instead,
    sidestepping whatever that internal coupling is.
  - Separately, `FilterBox` handles `TextField.Accepting` (Enter) by calling
    `IFilterable.FocusList()` - the same "jump into the list" action Esc-when-non-empty already
    performs - so a query can be typed and immediately worked with via Enter, without a second
    Tab/Down keystroke.

## Risks / Trade-offs

- [Every existing list's visual order changes from arrival/server order to alphabetical] →
  Accepted as the intended improvement; no existing spec promises arrival order, so nothing else
  breaks.
- [Master + filtered-projection shape adds internal state to `DrillableListView<T>`] → Contained
  entirely inside the base class; every existing external member (`ReplaceItems`, `SelectedItem`,
  `NeighborIdentity`, `Shortcuts`) keeps its current signature and contract.
- [Fuzzy-subsequence matching can over-match on short queries, e.g. "ab" matches most items in a
  list of similar names] → Accepted trade-off per explicit request (this is the intended
  `*o*c*e*` behavior, not literal substring); no match-highlighting or minimum-query-length in this
  change — worth revisiting later if it proves noisy in practice.
- [Replacing `ReplaceItems`' "first item" fallback with `NearestIdentity` changes behavior for
  every existing delete/refresh flow, not just the new search case] → Called out explicitly as a
  MODIFIED requirement (not silently folded into an ADDED one) so it's visible at spec-review time;
  behavior is strictly better in every case (a real neighbor vs. the top of a potentially huge
  list).

## Open Questions

- Should matched characters be visually highlighted in the list (e.g. bolded) to make fuzzy
  matches easier to parse at a glance? Deferred — out of scope for this first cut, worth
  revisiting once the plain version is in use.
