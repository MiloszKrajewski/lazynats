## Why

The status bar's fixed global-shortcut section is too wide for narrow terminals. Six of its
hints (`Alt-1`..`Alt-5` and `Alt-0`) repeat what every window title already shows through its
`N:` prefix (`1:Subscribe` ... `5:Templates`, `0:Live Feed`). Separately, About is the only global
shortcut on a function key (`F10`). Every other one is Alt-letter or bare punctuation (`?`), and
function keys have already been ruled out once as unreliable on laptops with Fn-lock (see the
`?` shortcut's history).

## What Changes

- Replace the six per-target `Alt-1..5`/`Alt-0` status bar hints with one display-only
  `Alt-#` hint labeled "Jump", shown as the **first** entry in the status bar.
- `Alt-1..5` and `Alt-0` stay bound and work exactly as before. Only their status bar hints go
  away.
- **BREAKING** (keybinding): the About dialog moves from `F10` to `Alt-A`. `F10` no longer opens
  it. The status bar hint becomes `Alt-A About` and stays visible.
- The `?` shortcut picker is unchanged: it still leaves out the top-level shortcuts, including
  the new `Alt-#` hint.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `tab-navigation`: adds a requirement for how the Alt+digit shortcuts appear in the status bar
  (one leading `Alt-#` "Jump" hint instead of one hint per target).
- `about-dialog`: the global shortcut changes from `F10` to `Alt-A`, and the status bar hint
  changes to match.

## Impact

- `src/lazynats/MainWindow.cs`: the `topLevelShortcuts` list, how status bar widgets are built
  from it (some entries are key-only and some are display-only), and the About binding. Also
  fixes the outdated comment claiming the picker is built from this list.
- `src/lazynats/Components/IShortcutSource.cs` (`ShortcutHint`), or a MainWindow-local
  equivalent, if a visibility flag lives there. See design.md.
- `doc/UI.md`: its About paragraph mentions `F10`.
- No NATS, Core, or test-project impact.
