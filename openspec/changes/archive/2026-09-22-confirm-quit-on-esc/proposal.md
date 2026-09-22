## Why

Esc does double duty: inside the app it means "back" (ascend a drill-down level, clear a filter,
close a dialog), but once nothing more specific claims it, it reaches the main window and quits
the whole app. Pressing Esc one time too many while backing out is an easy slip, and it throws
away the session (subscriptions, feed history, drill-down position) with no warning.

## What Changes

- An Esc that reaches the main window (i.e. nothing focused handled it as "back"/"cancel") no
  longer quits. It is swallowed: nothing happens.
- Alt+Q (the explicit Quit shortcut, advertised in the status bar) becomes the way to quit, and
  keeps quitting immediately with no prompt.
- Esc handling everywhere below the top level (dialogs, drill-down back, filter clear) is
  unchanged.

## Capabilities

### New Capabilities
- `quit-key`: how the app handles a top-level Esc (swallowed, no quit) and that Alt+Q
  is the explicit quit path.

### Modified Capabilities
<!-- none: no existing spec states Esc-quits behavior -->

## Impact

- `src/lazynats/MainWindow.cs`: swallow the quit key when it reaches the main window unhandled;
  Alt+Q binding untouched.
- No NATS-side, `lazynats.Core`, or dependency changes.
