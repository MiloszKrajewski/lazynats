## 1. Tab container in MainWindow

- [x] 1.1 Replace `subscriptionsFrame` in `src/lazynats/MainWindow.cs` with a `Terminal.Gui.Views.Tabs`
      at the same position/size the frame previously occupied; add the existing `SubscriptionsView`
      (unchanged) with `Title = "Subscriptions"` and set it as the initially selected (`Value`) tab;
      leave the live feed `FrameView` below untouched.
- [x] 1.2 Run the app to confirm `Tabs` renders and switches tabs correctly, and that the live
      feed pane's layout is unaffected, before building out the rest of the Publish tab (see
      design.md risk on unverified `Tabs` usage).

## 2. Publish tab fields

- [x] 2.1 Create `src/lazynats/PublishView.cs` with a Subject `TextField`, a headers section, a
      multi-line Payload `TextView`, and a Send `Button`, laid out per design.md.
- [x] 2.2 Implement the headers editor: a key `TextField` + value `TextField` input row (with an
      implicit `editingIndex`, null when composing new) above a `ListView` bound to an observable
      list of header pairs.
- [x] 2.3 Bind Ctrl+N to clear the input row and discard `editingIndex`; Ctrl+E to load the
      selected row's key/value into the input row and set `editingIndex` to its position; Ctrl+D
      to remove the selected row directly (clearing the input row too if it was being edited);
      `Accepted` on the value field (Enter) to append when `editingIndex` is null or overwrite
      that index otherwise, then clear the input row. Do **not** bind `Key.Delete` to row
      removal — leave it for normal text editing. Verify these commands work regardless of which
      header subview (key field, value field, list) currently has focus (see design.md risk on
      `KeyBindings` scoping).
- [x] 2.4 Confirm the exact `NatsConnection` publish API for sending a subject + headers +
      byte payload (`NATS.Client.Core`) — verify signature/overload before wiring Send in task 4.

## 3. Validation and visual styling

- [x] 3.1 Wire the Send `Button.Enabled` to Subject non-emptiness, re-evaluated as the Subject
      field changes.
- [x] 3.2 Apply a distinct (e.g. red) visual attribute to the Subject field while it is empty, to
      flag why Send is disabled.
- [x] 3.3 Prototype alternating-background banding on the headers list rows first and confirm it
      renders as expected, then apply the same banding to separate the Subject/Headers/Payload
      sections (design.md risk: no `Scheme`/`Attribute` customization exists elsewhere in this
      codebase yet).

## 4. Send behavior

- [x] 4.1 Implement the Send handler: publish a message on the entered subject with the entered
      header pairs and UTF-8 encoded payload text, via the `NatsConnection` obtained through
      `Services.Root` (same access pattern as `SubscriptionRegistry`).
- [x] 4.2 Report the outcome (success or failure) via the existing `StatusBar` in `MainWindow`,
      matching the transient-hint style already used for the `Clear` shortcut.
- [x] 4.3 Verify Subject/Headers/Payload remain populated after both a successful and a failed
      send.

## 5. Wire the Publish tab in

- [x] 5.1 Add `PublishView` (`Title = "Publish"`) to the `Tabs` from task 1, immediately after
      `SubscriptionsView`.
- [x] 5.2 Construct `PublishView` with its `NatsConnection` dependency via `Services.Root`, in
      `MainWindow`'s constructor alongside the other service lookups.

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 6.2 Manually run against a local/dockerized `nats-server` and confirm: headers can be
      added (Ctrl+N, Enter), edited in place (Ctrl+E, Enter), and removed (Ctrl+D) with no mouse
      use, and that Delete still edits text rather than removing a row; Send is disabled with the
      Subject field flagged red when Subject is empty; Send succeeds, reports status, and keeps
      all fields populated; a separate subscriber on the sent subject receives the message with
      the expected headers and payload.
