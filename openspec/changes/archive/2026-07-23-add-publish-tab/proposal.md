## Why

`doc/UI.md` calls for the ability to publish a NATS message (subject, headers, payload) without
leaving the terminal. Today there is no way to send a message at all — the app can only
subscribe and watch. This change adds that capability as a dedicated "Publish" tab, and
introduces tabbed navigation in the management area for the first time (previously a fixed
two-pane layout), which is also the pattern the planned Streams/Consumers/KV/OBJ tabs will
follow.

## What Changes

- Introduce a `Terminal.Gui.Views.Tabs` container in the management area (top pane) of
  `MainWindow`, replacing the single fixed `Subscriptions` frame. The live feed pane below stays
  as-is, outside the tabs.
- Add a **Publish** tab, positioned immediately after **Subscriptions**, with:
  - a Subject text field
  - a headers editor: keyboard-only add/remove list of key/value pairs (key/value input row,
    Enter adds, Delete removes the selected pair) — no buttons of any kind
  - a multi-line Payload text area (text only for now; JSON/hex/base64 payload types are a later
    stretch goal per `doc/UI.md`)
  - a Send button, disabled whenever Subject is empty, with the invalid state shown visually
    (e.g. red text/icon) rather than via a popup
  - on Send: publish the message on the connected `NatsConnection`, report success/failure via
    the status bar, and keep the form filled in for iterative resend
- Adopt flat, low-chrome visual styling for the new tab (alternating background bands to
  separate sections/rows instead of `FrameView` box borders) — a new visual pattern for this
  codebase, scoped to this tab for now.
- **BREAKING**: none (additive; existing Subscriptions behavior is unchanged, just relocated
  under a tab).

## Capabilities

### New Capabilities
- `nats-publish`: publishing a single NATS message (subject, headers, payload) from a dedicated
  Publish tab, including the keyboard-only headers editor and Subject-only validation gating
  Send.

### Modified Capabilities
- None. The Subscriptions screen moves from a fixed top-pane frame into the first tab of a new
  `TabView`, but no `nats-subscriptions` requirement (list/add/delete behavior) changes — only
  its presentation container, which is covered in design.md.

## Impact

- `src/lazynats/MainWindow.cs`: replaces the fixed `subscriptionsFrame` with a `TabView` hosting
  `SubscriptionsView` and the new `PublishView`.
- New `src/lazynats/PublishView.cs` (or similar): the Publish tab's fields, headers list, and
  Send button.
- `Services.cs` / `Program.cs`: no new singletons expected beyond the existing `NatsConnection`
  access needed to publish.
- `doc/UI.md`: already updated to describe the tab-based design (done during exploration, ahead
  of this proposal).
