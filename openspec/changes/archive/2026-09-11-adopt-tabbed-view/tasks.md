## 1. Copy TabbedView

- [x] 1.1 Copy `src/lazynats.Spike.Tabs/TabbedView.cs` into
      `src/lazynats/Components/TabbedView.cs` (the spike source stays untouched until step 6),
      updating its namespace to `lazynats.Components` and resolving any `using`/alias differences
      against this project's conventions (`Attribute`/`Color` aliasing already used in
      `MainWindow.cs`). Leave `AccentColor`/`DimColor`/`SelectedForegroundColor` and their generic
      literal defaults exactly as in the spike — `TabbedView` gains no reference to `Theme` or any
      other `lazynats` component.
- [x] 1.2 Add `TabAccentColor`, `TabDimColor`, and `TabSelectedForegroundColor` constants to
      `Theme.cs`, matching `EditableBackground`'s existing declaration style.
- [x] 1.3 Build `src/lazynats` and confirm `TabbedView` compiles cleanly in its new location with
      no leftover spike-only references.

## 2. Reimplement ManagementTabs on TabbedView

- [x] 2.1 Grep `src/lazynats` for any `Tabs`-specific API usage (events, members) outside
      `ManagementTabs.cs`/`MainWindow.cs`, to confirm nothing else depends on `Tabs` before its
      removal from `ManagementTabs`'s base.
- [x] 2.2 Change `ManagementTabs` to subclass the new `TabbedView` instead of `Tabs`; remove the
      `Command.Accept`/`Key.Tab` `TabGroup`-routing workaround and its class-level comment
      (FINDINGS.md task 4.4: a from-scratch control needs none). In `ManagementTabs`'s
      constructor, assign `Theme.TabAccentColor`/`Theme.TabDimColor`/
      `Theme.TabSelectedForegroundColor` onto the inherited `AccentColor`/`DimColor`/
      `SelectedForegroundColor` properties — this is the only place `lazynats`-specific
      customization of `TabbedView` happens.
- [x] 2.3 Reimplement `ManagementTabs.SelectTab(View tab)` against `TabbedView`'s
      `SelectedIndex`/`Select(int)` model, keeping its existing `View` parameter so `MainWindow`'s
      call sites (`tabs.SelectTab(subscribeTab)`, etc.) are unchanged.
- [x] 2.4 Add a tab-registration entry point on `ManagementTabs` (e.g. `Add(string caption, View
      content)` or equivalent) that forwards to `TabbedView.AddTab`, replacing the old
      `Tabs.Add(params View[])`/`Title`-property convention.
- [x] 2.5 Remove `FindFirstFocusableDescendant`, `FocusOwnHeader`, `FocusOwnContent`, `SwitchTab`
      overrides that duplicate logic `TabbedView` already implements natively, keeping only
      whatever `ManagementTabs`-specific behavior (if any) still differs.

## 3. Update MainWindow

- [x] 3.1 Change each tab's construction in `MainWindow.cs` to pass its `N:Title` caption (e.g.
      `"1:Subscribe"`) explicitly to the new registration entry point, instead of setting `Title`
      on the tab content view.
- [x] 3.2 Confirm `topLevelShortcuts`' Alt+N actions (`tabs.SelectTab(subscribeTab)`, etc.) and the
      `tabs.SelectTab(subscribeTab)` startup call still compile and behave unchanged against the
      new `ManagementTabs`.

## 4. Verify Behavior via tmux

- [x] 4.1 Launch the app in a detached `tmux` session and confirm the tab strip renders as one
      continuous bordered box with all five captions (`1:Subscribe`..`5:Templates`) visible,
      separated by `│`, bracketed by `┤...├`.
- [x] 4.2 Confirm the selected tab's caption is visually distinct from unselected captions, and
      the strip's border shows the accent color while focus is inside it (any tab's content or its
      header) and reverts when focus moves to the Live Feed or a dialog.
- [x] 4.3 Confirm Up/Down (content↔header), Left/Right (tab switching while header-focused),
      Alt+1..5 (direct select from anywhere, including from inside another tab's content), and
      Tab/Shift+Tab (cycling within a tab's own content, e.g. list↔FilterBox) all behave exactly
      as `tab-navigation`/`tab-scoped-list-shortcuts` already specify.
- [x] 4.4 Confirm mouse click on an unselected tab's caption selects it and focuses its content.
- [x] 4.5 Do one full interactive (non-tmux) pass to confirm color rendering looks correct (tmux
      color reporting has known gaps per `doc/focus-accent-color.md`). Confirmed manually.
- [x] 4.6 Extend the same focus-accent treatment to the Live Feed frame: add
      `FocusView` (`Components/`), a generic `FrameView` subclass factoring out
      `TabbedView`'s frame-only focus-accent mechanism (lazy `App.Navigation.FocusedChanged`
      subscribe from `OnDrawingAdornments`, whole-border recolor via `Border.GetOrCreateView()
      .SetScheme(...)`) without its caption/dim logic. Add `Theme.LiveFeedFocusAccentColor`
      (`ColorName16.BrightYellow`, matching `TabAccentColor`'s value under its own name per this
      file's one-constant-per-usage-site convention) and switch `MainWindow`'s `feedFrame` from
      `FrameView` to `FocusView`. Confirmed live via `tmux capture-pane -p -e`: the Live
      Feed frame's border renders the identical RGB as the tab strip's accented border while
      focused (Alt+0), and both revert to the dim gray border when focus moves away (Alt+1..5).

## 5. Clean Up

- [x] 5.1 Delete `src/lazynats.Spike.Tabs/` in its entirety (it was never referenced by
      `lazynats.sln` or `src/lazynats`, so no project-reference cleanup is needed).
- [x] 5.2 Confirm `dotnet build src/lazynats.sln` still succeeds after the deletion.
- [x] 5.3 Update `TODO.md` to remove the "different frame color for active window" item, now
      resolved by this change.

## Retroactive Notes

- The container swap turned out not to be purely mechanical: `TabbedView.Select` toggles a tab's
  `Visible` for Left/Right previewing *while keyboard focus stays on the header* (per
  `tab-navigation`'s "Tab Switching via Arrows Requires Header Focus"), unlike the old `Tabs`,
  where the previewed tab's content only ever became current once it actually held focus. That
  left `StreamsTab`/`ValuesTab`/`ObjectsTab`/`TemplatesTab`'s `OnHasFocusChanged`-gated initial
  load/detail-poll wiring showing stale/empty content on a Left/Right preview, so all four were
  changed to gate on `OnVisibleChanged`/`Visible` instead - `Visible` alone already captures
  "this is the selected tab," per `tab-navigation`. That same `Visible`-before-attachment ordering
  (`TabbedView.AddTab` sets `content.Visible = false` before `Add()`-ing the content) also meant
  `PollingDetailsView.SetActive` could be called before `App` was available, permanently losing its
  polling subscription; `SetActive` was changed to gate subscription creation on `App` being
  non-null instead of on `active` itself. Neither change was anticipated when this change's tasks
  were written; both were required to make the swap actually behavior-preserving per
  `tab-navigation`/`polling-details`, not a scope change beyond it.
