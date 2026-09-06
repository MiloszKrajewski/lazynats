## Why

Live Feed has no keyboard path to give it focus directly (Alt+1..5 jump straight into a
management tab's content, but the always-visible feed pane below has no equivalent), and its one
real shortcut (`Clear`, bound to `C`) predates the `IShortcutSource`/`ShortcutAggregator`
convention every other view now follows, so it never shows up in the `?` shortcut picker.

## What Changes

- Add `Alt+M` as a global shortcut (alongside the existing `Alt+1..5`/`Alt+P`/`Alt+Q` set in
  `MainWindow`) that moves keyboard focus directly into the Live Feed pane from anywhere in the
  app, mirroring how `ManagementTabs.SelectTab` already focuses a tab's content on `Alt+1..5`.
- Make `LiveUpdatesView` implement `IShortcutSource`, advertising its existing `Clear` (`C`)
  binding, so it participates in `ShortcutAggregator.Collect` like `DrillableListView` and
  `ListEditorView` already do.
- Remove the bespoke, pre-picker `clearShortcut` `Shortcut` widget and its `HasFocusChanged`
  visibility wiring from `MainWindow`'s status bar — `Clear` becomes discoverable via `?` only,
  consistent with every other per-view shortcut (none of which get a dedicated always-visible
  status-bar widget).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `live-feed`: adds a requirement that `Alt+M` focuses the Live Feed pane from anywhere, and a
  requirement that the feed's `Clear` shortcut is advertised via `IShortcutSource` and discoverable
  through the `?` picker.
- `shortcut-picker`: the hardcoded top-level shortcut set the picker excludes grows to include
  `Alt-M`; the spec's descriptive enumeration of that set needs updating to stay accurate.

## Impact

- `src/lazynats/MainWindow.cs`: add an `Alt+M` entry to `topLevelShortcuts`; remove the
  `clearShortcut` widget, its `HasFocusChanged` subscription, and its status-bar registration.
- `src/lazynats/LiveFeed/LiveUpdatesView.cs`: implement `IShortcutSource`.
- No changes to `ShortcutAggregator`, `ShortcutPickerDialog`, or the feed pipeline itself.
