## Why

Publish currently lives in its own management tab, and the Payload field needed a Navigate/Edit
toggle (Ctrl+E to enter, Esc to leave) purely to keep Tab available for band-to-band focus
movement instead of being swallowed by the payload `TextView`. `CreateKeyDialog`'s Value field
(KV entry New/Edit) shows this gate was never necessary: setting `TabKeyAddsTab = false` on the
`TextView` stops it from consuming Tab at all, so focus advances normally with no extra mode.
Composing and sending a single message is also a one-off action, not something that benefits
from a permanently-reserved tab — a dialog reachable from anywhere fits the action better and
frees the tab strip for the drill-down management views.

## What Changes

- **BREAKING**: Remove the "Publish" management tab. Composing and sending a message moves to a
  modal Publish dialog, opened from anywhere via Alt+P, with Subject, Headers, and Payload fields
  and Cancel/Send buttons.
- **BREAKING**: Renumber the remaining management tabs and their Alt+digit shortcuts to close the
  gap left by Publish: Subscribe stays `1`/Alt+1, Streams becomes `2`/Alt+2, KV becomes `3`/Alt+3,
  OBJ becomes `4`/Alt+4.
- Drop the Payload Navigate/Edit gate entirely: the Payload field is always directly editable,
  and Tab/Shift-Tab/arrow keys move focus between Subject, Headers, Payload, and Send the same
  way they do for every other field, using `TabKeyAddsTab = false` (the same mechanism
  `CreateKeyDialog`'s Value field already relies on) instead of a mode toggle.
- Sending no longer closes the dialog: Send publishes and reports success/failure inline in the
  dialog while keeping Subject/Headers/Payload populated, so the same message can be tweaked and
  resent without reopening the dialog. Cancel (or Esc) closes the dialog without publishing.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nats-publish`: Composing and sending a message moves from an always-present management tab to
  a modal dialog opened via Alt+P; the Payload Navigate/Edit gate requirement is removed in favor
  of always-editable Payload text; Send no longer closes the dialog, and the dialog reports send
  feedback inline instead of via the main status bar.
- `tab-navigation`: The "Publish" tab entry and its `2:Publish`/Alt+2 assignment are removed;
  Streams/KV/OBJ shift down to positions `2`/`3`/`4` and Alt+2/Alt+3/Alt+4 respectively.

## Impact

- `src/lazynats/Publish/PublishTab.cs` is replaced by a new `PublishDialog` (the header
  editor/dialog pieces — `HeaderEditorView`, `HeaderDialog`, `HeaderColonPresenter` — are reused
  as-is).
- `src/lazynats/MainWindow.cs`: drop the Publish tab and its tab shortcut/status-bar wiring, add
  an Alt+P global shortcut that opens `PublishDialog`, renumber the remaining tab titles and
  Alt+digit shortcuts.
- `doc/UI.md`: update the tab table and the "Sending messages" section to describe the dialog
  instead of a tab.
