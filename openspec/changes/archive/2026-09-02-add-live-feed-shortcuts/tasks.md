## 1. Focus-jump shortcut

- [x] 1.1 Add an `Alt+M` entry ("Live Feed") to `MainWindow`'s `topLevelShortcuts`, with an action
      that calls `liveUpdates.SetFocus()`.
- [x] 1.2 Verify via `tmux` (per CLAUDE.md) that `Alt+M` moves focus into the Live Feed pane from
      each management tab, and that the feed's `ListView` visibly shows focus.

## 2. Discoverable Clear shortcut

- [x] 2.1 Make `LiveUpdatesView` implement `IShortcutSource`, exposing
      `Shortcuts => [new ShortcutHint(Key.C, "Clear", Clear)]`.
- [x] 2.2 Remove the `clearShortcut` `Shortcut` widget, its `HasFocusChanged` subscription, and its
      status-bar registration from `MainWindow`.
- [x] 2.3 Verify via `tmux` that pressing `?` while the Live Feed pane is focused lists `Clear`,
      and that the status bar no longer shows a dedicated `Clear` widget.

## 3. Spec sync

- [x] 3.1 Confirm `openspec/specs/live-feed/spec.md` and `openspec/specs/shortcut-picker/spec.md`
      read correctly once this change's deltas are applied (via `openspec-sync-specs`/archive), in
      particular that the `shortcut-picker` spec's top-level-shortcut enumeration reads `Alt-1..5,
      Alt-M, Alt-P, Alt-Q`.
