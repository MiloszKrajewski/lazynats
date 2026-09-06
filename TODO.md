# TODO

- **Tab key dropped after Shift-Tab into a TextField with selected text**: when a `TextField`
  regains focus via Shift-Tab and auto-selects its existing text (`TextField.OnHasFocusChanged`'s
  select-all-on-focus behavior), the very next Tab keypress is silently dropped before it even
  reaches the application's key handler (confirmed via `App.Keyboard.KeyDown` never firing for
  it), so focus doesn't advance until Tab is pressed again; any other key (arrow, letter) in that
  same state is received normally and "unsticks" the next Tab. Reproduces identically on the
  pre-`publish-payload-edit-mode` baseline, so it's a pre-existing Terminal.Gui/console-driver
  quirk, not a regression from that change.
