## 1. Resolve the Jump widget's key rendering

- [x] 1.1 Check Terminal.Gui v2's `Shortcut` API (context7 first) for how to show `Alt-#` in the
      key column without registering a working key binding. Record the chosen approach in
      design.md's Open Questions.

## 2. Split top-level shortcuts by role

- [x] 2.1 In `MainWindow.cs`, move Alt-1..5 and Alt-0 into their own `jumpShortcuts` list, and
      keep Quit, Publish, About, and `?` in the global list
- [x] 2.2 Make the `KeyDown` loop match against both lists, so every existing binding still works
- [x] 2.3 Build the status bar widgets as: the display-only `Alt-# Jump` widget first (no
      `Action`), then the global shortcuts, then the existing tab status widgets
- [x] 2.4 Replace the outdated comment claiming the picker is built from this list with one that
      describes the two lists' roles

## 3. Move About to Alt-A

- [x] 3.1 Change the About `ShortcutHint` key from `Key.F10` to `Key.A.WithAlt`, and update the
      "F10" mention in its re-entrancy comment
- [x] 3.2 Update `doc/UI.md`'s About paragraph (`F10` becomes `Alt+A`)

## 4. Verify

- [x] 4.1 `dotnet build src/lazynats.sln` compiles cleanly
- [x] 4.2 Use tmux to check that the status bar reads `Alt-# Jump` first, with no per-digit
      hints, and that `Alt-A About` is present
- [x] 4.3 Use tmux to check that `M-1`..`M-5` and `M-0` still switch tabs and focus the live
      feed, that `M-a` opens About, and that `F10` does nothing
- [x] 4.4 Use tmux to check that `Alt-A` inside an open dialog (e.g. Publish) doesn't open About,
      and that typing `#` in a text field (e.g. the Subscribe pattern) still inserts `#`
