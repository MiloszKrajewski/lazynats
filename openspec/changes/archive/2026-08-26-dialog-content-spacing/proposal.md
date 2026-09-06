## Why

`Window`/`FrameView` titles already follow the "leading and trailing space" convention (CLAUDE.md:
`" Live Feed "`, MainWindow's tab titles) so text doesn't crowd the border corners, and
`Dialog<T>` subclasses already set `Padding.Thickness = new Thickness(1, 0, 1, 0)` so field
content doesn't butt against the left/right border. But that convention stops short of modal
dialogs' own title text and `MessageBox`'s title/message text: `CreateStreamDialog`/
`CreateConsumerDialog`/`PatternDialog`/`HeaderDialog` all set `Title` with no leading/trailing
space, and every `MessageBox.Query`/`ErrorQuery` call (confirmations, error reports) passes both
title and message with no horizontal breathing room at all — `MessageBox` auto-sizes tightly
around its longest line, so text can render flush against the border, and exception messages in
particular read as cramped.

## What Changes

- Extend the existing "leading/trailing space in title" convention to every `Dialog<T>` subclass's
  `Title` (`CreateStreamDialog`, `CreateConsumerDialog`, `PatternDialog`, `HeaderDialog`) and to
  every `MessageBox.Query`/`ErrorQuery` call site's title.
- Add the same horizontal breathing room to `MessageBox`'s message/body text, and confirm
  `Dialog<T>`'s existing `Padding.Thickness` gives the same treatment to its own body content
  (fields, labels) — widening it if a single-space `Thickness(1, 0, 1, 0)` reads as insufficient
  once titles/messages are compared side by side.
- Since `MessageBox` and `Dialog<T>` both auto-size (or are explicitly sized) around their
  content, padding text out on both sides means the box itself needs to grow to match — this
  change covers whatever sizing adjustment keeps padded text from being clipped or re-cramping the
  box.
- No behavioral change to any dialog's fields, buttons, keybindings, or commit/cancel logic —
  purely spacing/rendering.

## Capabilities

### New Capabilities

- `dialog-spacing`: modal dialogs (`Dialog<T>` subclasses) and `MessageBox` prompts SHALL give
  their title and body/message text leading and trailing horizontal space, so text never renders
  flush against the border — the modal-dialog counterpart to the existing bordered-container
  title-spacing convention already documented in CLAUDE.md for `Window`/`FrameView`.

### Modified Capabilities

(none — no existing spec currently documents dialog title/body spacing)

## Impact

- `Streams/CreateStreamDialog.cs`, `Streams/CreateConsumerDialog.cs`,
  `Subscriptions/PatternDialog.cs`, `Publish/HeaderDialog.cs` — `Title` assignments.
- `Streams/StreamsTab.cs`, `MainWindow.cs` — every `MessageBox.Query`/`ErrorQuery` call site's
  title and message arguments (including the new Delete Consumer prompt added by
  `add-consumer-delete`, if that change lands first — otherwise this change's own list stays in
  sync when it does).
- No new files; no NATS/Terminal.Gui dependency changes.
