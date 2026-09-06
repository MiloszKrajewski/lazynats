## Why

`doc/UI.md` reserves a "Streams" management tab (Alt+3) for browsing JetStream durable streams,
but no code exists for it yet — only Subscribe and Publish are built. `doc/stream-tab-UI.md`
records a design conversation that broke the full tab (stream list, consumer drill-down, delete,
create/edit) into small, independently shippable slices to avoid scope explosion. This change is
slice 1: the smallest useful piece, a flat, read-only stream list with a live-refreshing detail
panel — nothing else the tab needs (consumer drill-down, delete, create/edit) hangs off it, so it
can land and be used on its own before any of that exists.

## What Changes

- Add a `3:Streams` management tab, switchable via Alt+3, following the existing tab-title and
  shortcut conventions used by Subscribe/Publish.
- Add `StreamListView`: a flat, read-only list of JetStream stream names, fetched once on tab
  entry and refreshed only on demand via Ctrl+R (not polled on a timer, to avoid a jumpy/
  reordering list and unnecessary server load).
- Add `StreamDetails`: an RHS info panel showing the highlighted stream's info (config + current
  state — messages, bytes, first/last seq, retention, replicas, etc.), refreshing periodically
  while a stream stays highlighted, not only on selection change. This is the only part of the
  tab that polls the server.
- No consumer drill-down, no Esc/Backspace navigation, no delete, no create/edit — all explicitly
  deferred to later slices per `doc/stream-tab-UI.md`.

## Capabilities

### New Capabilities
- `nats-streams`: read-only listing of JetStream streams and periodically-refreshed detail view
  of the highlighted stream, per the `StreamsTab`/`StreamListView`/`StreamDetails` shape in
  `doc/stream-tab-UI.md`.

### Modified Capabilities
- `tab-navigation`: adds the `3:Streams` tab title and its Alt+3 shortcut alongside the existing
  Subscribe/Publish tabs; the focus-climbing (Up/Down) and header-driven Left/Right switching
  requirements already generalize to any tab count, no behavioral change there.

## Impact

- New files under a `Streams/` folder (mirroring `Subscriptions/`): `StreamsTab.cs`,
  `StreamListView.cs`, `StreamDetails.cs`.
- `MainWindow.cs`: resolve dependencies, construct `StreamsTab`, register it with
  `ManagementTabs`, per `openspec/specs/tab-content-structure/spec.md` (registration only, no
  layout assembly there).
- `Program.cs`/`Services.cs`: register whatever JetStream context/stream-listing service
  `StreamsTab` needs (`NATS.Client.JetStream` is already referenced but unused).
- No changes to `SubscriptionRegistry`, the live feed pipeline, or existing tabs.
