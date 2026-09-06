## Why

The Streams tab's consumer level is currently read-only (`nats-streams`' "Read-Only List"
requirement) — the only way to create a JetStream consumer is outside the app (`nats consumer
add`, another client). The Stream level already got the same treatment in `add-stream-create`
(Create + Delete via a modal dialog with an Essential/Advanced split); doing the same for
consumers closes the gap and lets a user go from "no consumer" to "usable pull consumer" without
leaving the terminal.

## What Changes

- Add Ctrl+N to the consumer-level list (`Streams/ConsumerListView.cs`), opening a modal dialog
  that creates a consumer on the currently drilled-into stream (the stream is fixed context, not
  a dialog field — mirrors how the consumer list itself is already scoped to `_currentStream`).
- New `Streams/NewConsumerOptions.cs`: a dialog-owned result record (nullable = "unset") with a
  `ToConsumerConfig()` translator, following the `NewStreamOptions`/`ToStreamConfig()` pattern
  exactly — including auditing `ConsumerConfig`'s own CLR-default sentinels (`MaxDeliver`,
  `MaxAckPending`, `AckWait` all default to `0`/`TimeSpan.Zero` from a bare `new ConsumerConfig()`)
  before deciding what `ToConsumerConfig()` needs to set explicitly.
- New `Streams/CreateConsumerDialog.cs`, an Essential-only multi-field modal (Name, Filter
  Subjects, Ack Policy, Deliver Policy limited to All/Last/New/LastPerSubject) — same shape as
  `CreateStreamDialog`: per-field validation, single `Create` button, Esc cancels, reopen
  pre-seeded on server-side failure. Name is required — every consumer created here is durable;
  ephemeral creation is excluded (not deferred), since a server-generated name the user can't act
  on plus no exposed `InactiveThreshold` means an ephemeral consumer would likely be
  auto-deleted before anything could use it. Filter Subjects is a delimiter-separated
  (space/comma/semicolon) text field, same parsing as Streams' own Subjects field, reusing
  `NewStreamOptions.ParseSubjects` directly rather than duplicating it — `ToConsumerConfig()`
  always writes through `ConsumerConfig`'s plural `FilterSubjects` field, never the singular
  `FilterSubject`, matching how `K4os.NatsTransit` (a real, independent JetStream consumer)
  creates its own consumers regardless of filter count.
- Consumers created by this dialog are always pull consumers (`DeliverSubject` left unset) — push
  fields (`DeliverSubject`, `DeliverGroup`, `IdleHeartbeat`, `FlowControl`, `RateLimitBps`) are
  out of scope entirely, not just deferred to Advanced, since nothing in the app consumes via a
  push subscription today.
- **Modifies `nats-streams`' "Read-Only List" requirement**: the consumer-level guarantee narrows
  from "no mutation affordance" to "no edit or delete affordance" — Create is now present at the
  consumer level, matching the stream level's existing Create+no-Edit shape.

## Capabilities

### New Capabilities
(none — this extends the existing Streams tab capability)

### Modified Capabilities
- `nats-streams`: "Read-Only List" requirement's consumer-level scenario changes from "no
  mutation affordance" to "no edit or delete affordance" (Create is added). New requirements for
  Create Consumer and Create Consumer Field Validation, mirroring the existing Create Stream /
  Create Stream Field Validation requirements.

## Impact

- `Streams/ConsumerListView.cs`: gains `Command.New` + `Key.N.WithCtrl`, a `CreateRequested`
  event — same shape as `StreamListView`'s existing Ctrl+N wiring.
- `Streams/StreamsTab.cs`: handles `CreateRequested` by opening `CreateConsumerDialog` scoped to
  `_currentStream`, calling `_jetStream.CreateConsumerAsync`, and refreshing+highlighting via
  `_consumerListView.ReplaceItems(...)` — same control flow as `TryCreateStreamAsync`.
- New files: `Streams/NewConsumerOptions.cs`, `Streams/CreateConsumerDialog.cs`.
- `openspec/specs/nats-streams/spec.md`: requirement changes as described above.
- No changes to `StreamDetails`/`ConsumerDetails`, KV/Obj tabs, or any other capability.
