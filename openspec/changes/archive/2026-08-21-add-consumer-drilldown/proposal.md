## Why

The Streams tab (`nats-streams`) currently only lists streams and shows stream detail — it stops
short of the drill-down `doc/UI.md` and `doc/stream-tab-UI.md` both call for: pressing Enter on a
stream should descend into that stream's consumers, with Esc/Backspace climbing back up. Without
this, the tab can show *that* a stream has consumers (via its `Consumers` count) but not what they
are or their delivery/ack state — the main reason someone would reach for a JetStream consumer
inspector in the first place.

## What Changes

- Enter on a highlighted stream in the Streams tab descends into a `ConsumerListView` listing that
  stream's consumers (fresh `ListConsumersAsync` fetch every descent); Esc/Backspace climbs back up
  to the stream list without re-fetching it.
- A `ConsumerDetails` panel shows config + state for the highlighted consumer, polling
  `GetConsumerAsync` every 3s while shown — mirroring `StreamDetails`' existing behavior, built as
  a near-identical (deliberately duplicated, not abstracted) sibling component.
- The `StreamsTab` breadcrumb/label switches between stream-level and consumer-level text
  (`"Streams"`/`"Details"` vs. `"Consumers of <name>"`/`"Consumer Details"`) so the current level is
  always obvious, per `doc/UI.md`'s requirement.
- New reusable Rx-based polling infrastructure in `Core/AsyncExtensions.cs`:
  - `SelectAsync` (`Select` + `Concat` semantics — sequential async projection, never overlapping,
    nothing dropped).
  - `ObserveOnApp` (marshals `IObservable<T>` notifications onto Terminal.Gui's `App.Invoke`,
    replacing the ad-hoc `App?.Invoke(...)` pattern currently duplicated across `LiveUpdatesView`,
    `PublishTab`, and `StreamsTab`).
  - Both `StreamDetails` and `ConsumerDetails` are refactored onto this: each owns its own
    always-running 3s timer, gated by an `_active` flag (`SetActive(bool)`, pushed by `StreamsTab`)
    so the pipeline issues zero NATS calls while its level isn't shown or the tab isn't selected —
    without needing to start/stop the timer itself.
- `nats-streams`'s read-only list requirement is unaffected — no create/edit/delete is added at
  either level; this is drill-down navigation and detail viewing only.
- `<Messages>` virtual-node browsing and consumer-level `<Pending>` drill-down remain explicitly
  out of scope (per `doc/stream-tab-UI.md`).

## Capabilities

### New Capabilities
- `reactive-polling`: the `Core/AsyncExtensions.cs` `SelectAsync`/`ObserveOnApp` operators and the
  active-gated, always-running poll-timer pattern built on them. Framed as its own capability
  because it's deliberately generic (not streams-specific) and intended for reuse by the live feed
  pipeline in a later, separate change.

### Modified Capabilities
- `nats-streams`: adds consumer-level requirements (consumer list, consumer detail panel, periodic
  consumer detail refresh, consumer list refresh-on-descend) and the Enter/Esc navigation contract
  between stream level and consumer level, including the breadcrumb requirement.

## Impact

- New: `src/lazynats/Streams/ConsumerListView.cs`, `ConsumerDetails.cs`, `ConsumerNamePresenter.cs`,
  `src/lazynats/Core/AsyncExtensions.cs`.
- Modified: `src/lazynats/Streams/StreamsTab.cs` (level state, breadcrumb, wiring
  descend/ascend), `src/lazynats/Streams/StreamListView.cs` (Enter → descend via `Accepted`),
  `src/lazynats/Streams/StreamDetails.cs` (refactored onto the new poll pipeline).
- Dependency: `System.Reactive` (already referenced in `lazynats.csproj` and AOT-probed via
  `src/lazynats.AotProbe/Probes/RxProbe.cs`) gets its first real consumer in the app.
- No change to `NATS.Client.JetStream` package version (2.8.2) — `ListConsumersAsync`/
  `GetConsumerAsync` already exist on `INatsJSContext`.
