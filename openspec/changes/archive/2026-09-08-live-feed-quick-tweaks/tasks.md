## 1. Live Feed shortcut rebind

- [x] 1.1 In `MainWindow.cs`'s `topLevelShortcuts`, change the Live Feed `ShortcutHint`'s key from
      `Key.M.WithAlt` to `Key.D0.WithAlt` (label stays "Live Feed").
- [x] 1.2 Verify via tmux: `Alt+0` moves keyboard focus into the Live Feed pane from a management
      tab, and `Alt+M` no longer does anything.

## 2. Message Detail headers color

- [x] 2.1 In `MessageDetailDialog.cs`, capture the headers frame's content view (replace the `_`
      discard in `EditFrame.CreateReadOnly(headersText, headerFrameY, headerFrameHeight, out _)`
      with a named variable).
- [x] 2.2 Apply `headerView.SetScheme(new Scheme(new Attribute(Theme.HeaderColor,
      Theme.EditableBackground)))`, mirroring the existing `subjectView.SetScheme(...)` call.
- [x] 2.3 Verify via tmux: opening the Message Detail dialog for a message with headers shows the
      Headers section in green (matching the live feed row's header color), while Subject stays
      cyan and Payload stays default.

## 3. Live Feed frame title

- [x] 3.1 In `MainWindow.cs`, change the Live Feed `FrameView`'s `Title` from `" Live Feed "` to
      `" 0:Live Feed "`, matching the `N:Title` pattern the numbered management tabs use.
- [x] 3.2 Verify via tmux: the Live Feed frame's border shows "0:Live Feed".

## 4. Spec sync

- [x] 4.1 Sync `specs/live-feed/spec.md`'s delta into `openspec/specs/live-feed/spec.md`, renaming
      the "Alt+M Focuses the Live Feed" requirement to "Alt+0 Focuses the Live Feed", updating both
      scenarios' key references, and updating the "No In-View Header" scenario's host-frame title
      reference to "0:Live Feed".
