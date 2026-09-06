# Keyboard focus navigation — findings and current state

Status: working, verified by hand via `tmux` across all three drillable-list tabs
(`add-drillable-list-search`), but arrived at empirically rather than from documented Terminal.Gui
behavior — several of the underlying mechanisms below aren't fully explained by the public docs
(`doc/terminal-gui-howto.md`'s canonical sources) and were only pinned down by reading Terminal.Gui
2.4.10's actual source and reproducing behavior directly. Good enough to leave alone for now; this
doc exists so a future pass doesn't have to re-derive it.

## Why this became a problem now

Every tab's content used to have exactly one focusable child (a `DrillableListView<T>`, wrapped in
an `EditFrame`). Adding `FilterBox` (`Components/FilterBox.cs`) as a second, sibling focusable View
per level exposed several gaps in Terminal.Gui's default Tab/Shift+Tab/Up/Down handling that a
single-focusable-child tab never hit. `ManagementTabs.cs`'s class-level comment already described
one such gap for arrow keys (fixed pre-existing); the FilterBox work hit several more.

## What we found, reading Terminal.Gui 2.4.10 source directly

Source browsed at `github.com/tui-cs/Terminal.Gui` tag `v2.4.10`, notably
`Terminal.Gui/Views/Tabs.cs`, `Terminal.Gui/ViewBase/View.Navigation.cs`,
`Terminal.Gui/App/ApplicationNavigation.cs`, and `Terminal.Gui/Views/TextInput/TextField/`.

- **`Tabs` marks itself `TabStop = TabBehavior.TabGroup`** in its own constructor, and
  **`Tabs.OnSubViewAdded` forcibly overwrites every added tab page's `TabStop` to plain
  `TabBehavior.TabStop`** (and its `Border.View`'s to `NoStop`), regardless of what was set on the
  page beforehand. Setting `TabStop = TabGroup` on a tab's content view *before* `Tabs.Add()` is
  therefore silently clobbered the moment `Add()` runs — dead code, not a working per-tab
  boundary. (We tried this; it does nothing.)
- **Tab/Shift+Tab do not bubble through per-view `KeyBindings` the way every other key does.**
  Ctrl+R, Esc, Up/Down (`Command.Up`/`Down`/`Left`/`Right`, as `ManagementTabs` already
  overrides) all resolve by walking from the focused view up through each ancestor's own
  `KeyBindings` table, same as `doc/terminal-gui-howto.md` gotcha #4 describes. Tab/Shift+Tab
  don't: confirmed empirically (via a temporary `File.AppendAllText` debug log, since this isn't
  otherwise observable) that **neither a `KeyDown` event handler nor an `AddCommand`+
  `KeyBindings.Add` pair on `FilterBox` itself is ever invoked** for a Tab/Shift+Tab keypress,
  even though the exact same pattern reliably works for every other key on every other view in
  this app. They appear to route directly to the nearest enclosing `TabBehavior.TabGroup` — which,
  given the point above, is always `ManagementTabs`/`Tabs` itself, never a tab's own content. A
  binding placed anywhere *below* that (on `FilterBox`, `DrillableListView<T>`, or a `*Tab.cs`)
  is structurally unreachable for these two keys.
  - Corollary: Terminal.Gui's own default handling of "reached the end of a page's focus chain"
    for Tab/Shift+Tab is apparently to escape to the tab's own header, or even a *different*
    management tab's content — the same class of leak `ManagementTabs`'s pre-existing comment
    describes for arrow keys, just for Tab instead, and not fixable the same way (see above).
- **`Command.NextTabStop`/`Command.PreviousTabStop` are individually cursed in this version**,
  independent of the point above. Even after correctly binding `Key.Tab`/`Key.Tab.WithShift` on
  `ManagementTabs` (which *does* work — the KeyBindings dispatch problem is specifically about
  *which view*, not about these two keys being unreachable in general) and replacing their default
  handlers, we reproduced *repeatedly*: once `Command.PreviousTabStop`'s handler fires one time in
  a session, `Command.NextTabStop`'s handler **silently stops being invoked at all** for the rest
  of the session — confirmed via the same debug log (zero log lines for every subsequent Tab
  press, while Shift+Tab kept logging normally, indefinitely). This reproduced with a synchronous
  handler and with the focus-change deferred one main-loop tick via `App.Invoke` — deferral didn't
  help, ruling out a naive reentrancy explanation. Root cause not identified (would need to step
  through Terminal.Gui's own internals with a debugger, not just read the source); worked around
  by **not using these two `Command` values at all** — both keys are bound to the same,
  otherwise-unused `Command.Accept` instead, which doesn't exhibit the issue.
- **Up/Down arrows are ordinary keys** (not special-cased like Tab/Shift+Tab) and *do* bubble
  through per-view `KeyBindings` normally — a `Command.Up`/`Command.Down` handler placed on
  `DrillableListView<T>`/`FilterBox` themselves works exactly as expected, first try, no surprises.
- **Two independent mechanisms decide which view gets focus when a tab becomes active**, and they
  must be kept in sync by hand:
  1. `ManagementTabs.FocusOwnContent()` — Down-arrow from a focused header.
  2. Direct `tabs.Value = someTab` assignment (what `Tabs`'s own `Value` setter does when
     something else sets it) — used by every `Alt+1..4` shortcut and the initial tab in
     `MainWindow.cs`.
  Only (1) went through `ManagementTabs`'s own `FindFirstFocusableDescendant` DFS; (2) used
  whatever `Tabs.Value`'s setter does internally (its own default-focus resolution, apparently
  unaware of `FilterBox`'s deliberate Add()-order-before-its-list placement). Fixed by introducing
  `ManagementTabs.SelectTab(tab)` — `Value = tab` plus an explicit
  `FindFirstFocusableDescendant(tab).SetFocus()` — and routing *every* direct-selection call site
  through it instead of touching `Value` directly.
- **`TextField`'s built-in `Autocomplete` was a red herring.** Its default `AppendAutocomplete`
  strategy does bind `SelectionKey = KeyCode.Tab`, but `TextField` actually uses a *different*
  default (`TextFieldAutocomplete : PopupAutocomplete`, `SelectionKey` defaults to `Key.Enter`),
  and `TextField.OnKeyDown` gates the whole autocomplete call behind
  `Autocomplete?.Suggestions.Count > 0` — with zero suggestions (our case, no generator populated),
  it's never even invoked. Worth remembering next time Tab does something unexpected in a
  `TextField` — it's tempting to blame Autocomplete, but check `Suggestions.Count` first.

## Current implementation (what to read to pick this back up)

- `Components/FilterBox.cs` — the search field. `IFilterable` is the narrow interface it uses to
  talk back to whatever list it's attached to (`ApplyFilter`/`FocusList`/`HandleEmptySearchEscape`/
  `AttachedFilterBox`), without depending on `DrillableListView<T>` directly. Binds `Command.Down`
  directly (works fine, ordinary key). Does **not** attempt to bind Tab/Shift+Tab on itself — see
  above for why that's pointless.
- `Components/DrillableListView.cs` (`AttachFilterBox`) — binds `Command.Up` directly to focus the
  attached box (ordinary key, works fine). Implements `IFilterable`, including
  `AttachedFilterBox => _filterBox`.
- `Components/ManagementTabs.cs`:
  - `AdvanceWithinPage()` — the only place Tab/Shift+Tab for a list↔FilterBox pairing can be
    intercepted at all. Walks up from `App.Navigation.GetFocused()` (not `Value`'s immediate
    child — needs the *actually* focused leaf) looking for a `FilterBox` or an `IFilterable`, and
    focuses the other half of the pair directly, never calling Terminal.Gui's own `AdvanceFocus`
    for this case. Bound to `Command.Accept` via explicit `KeyBindings.Add(Key.Tab, ...)` /
    `Key.Tab.WithShift`, not `NextTabStop`/`PreviousTabStop` (see the cursed-commands point above).
    Falls back to `Application.Navigation.AdvanceFocus(Forward, TabStop)` for anything outside a
    pairing (Subscribe/Publish tabs today — untested with this specific navigation work, since
    neither has a FilterBox; if Tab/Shift+Tab ever misbehaves there, this fallback path is where
    to look first).
  - `SelectTab(tab)` — see the "two independent mechanisms" point above. Every direct tab-selection
    call site (`MainWindow.cs`'s `Alt+1..4` shortcuts, the initial tab) must go through this, not
    `Value = tab`.
  - `FindFirstFocusableDescendant` — explicitly skips any `sub is FilterBox` so default-focus
    always lands on the list, independent of `FilterBox` being Add()-ed first (for Tab-order) or
    second.
- Each `*Tab.cs` (`StreamsTab`, `ValuesTab`, `ObjectsTab`) `Add()`s a level's `FilterBox` *before*
  its list `EditFrame`, matching the box's on-screen position above the list — this is what makes
  Tab-order feel spatial. This ordering is exactly what makes `FindFirstFocusableDescendant`'s
  FilterBox-skip necessary; don't reorder one without checking the other.

## Loose ends / things to revisit

- The root cause of the `NextTabStop`/`PreviousTabStop` one-way breakage was never actually found,
  just worked around. If a future Terminal.Gui upgrade changes this behavior, the `Command.Accept`
  hijack could be revisited (try switching back to the "correct" commands first, in isolation,
  before assuming the workaround is still needed).
- `Command.Accept` is a real, meaningful command elsewhere in Terminal.Gui (button/dialog default
  action). Binding it to Tab/Shift+Tab on `ManagementTabs` specifically hasn't caused an observed
  conflict, but it's an arbitrary reuse of an existing enum value, not a purpose-built one (the
  `Command` enum is closed — can't add new members) — worth a second look if a future Accept-based
  interaction (a button living directly inside `ManagementTabs`'s own content, say) ever behaves
  oddly.
- Subscribe/Publish tabs were never exercised by this investigation (no `FilterBox`, so no reason
  to). If either ever grows a second focusable child, expect to hit the same
  Tab/Shift+Tab-bubbling gap and need the same `AdvanceWithinPage`-style fix, generalized.
- Everything here is scoped to Terminal.Gui 2.4.10 specifically (see
  `src/lazynats/lazynats.csproj`); re-verify against the installed version before trusting any of
  this after an upgrade — check `~/.nuget/packages/terminal.gui/<version>/lib/*/*.xml` and, if
  needed, re-fetch source the same way (`github.com/tui-cs/Terminal.Gui` tag matching the
  installed version) rather than assuming these notes still hold.
