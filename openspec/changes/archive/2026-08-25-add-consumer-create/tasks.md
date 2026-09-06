## 1. NewConsumerOptions and translation

- [x] 1.1 Create `Streams/NewConsumerOptions.cs`: `internal sealed record NewConsumerOptions(
      string Name, IReadOnlyList<string> FilterSubjects, ConsumerConfigAckPolicy AckPolicy,
      ConsumerConfigDeliverPolicy DeliverPolicy)` — `Name` required (every consumer created here
      is durable; see design.md's Decisions for why ephemeral is excluded, not just deferred)
- [x] 1.2 Add `ToConsumerConfig()` on/for `NewConsumerOptions` (same file) that maps to
      `NATS.Client.JetStream.Models.ConsumerConfig`: always `new ConsumerConfig(Name.Trim())` (sets
      `Name`/`DurableName` together per the ctor's own doc comment) plus `AckPolicy`,
      `DeliverPolicy` from the record; `FilterSubjects` is always written through the plural
      `ConsumerConfig.FilterSubjects` field (`FilterSubjects.Count > 0 ? FilterSubjects : null`),
      never the singular `FilterSubject` — matches `K4os.NatsTransit`'s real-world usage (see
      design.md's Decisions); this dialog never sets the singular field. Add a comment noting
      that, unlike `ToStreamConfig()`, no other field needs an explicit sentinel default —
      `ConsumerConfig`'s remaining numeric/duration properties are all `[JsonIgnore(Condition =
      WhenWritingDefault)]`, so a CLR-default value on any of them is omitted from the request
      rather than sent as a literal `0`, per design.md's Context section
- [x] 1.3 Reuse `NewStreamOptions.ParseSubjects(string)` for the Filter Subjects field's parsing —
      no new parsing helper needed. Name's only validation is non-empty-after-trim (inline, same
      as `NewStreamOptions.Name`'s check)

## 2. CreateConsumerDialog

- [x] 2.1 Create `Streams/CreateConsumerDialog.cs` as `Dialog<NewConsumerOptions>` with labeled
      fields: Name (`TextField`), Filter Subjects (`TextField`), Ack Policy
      (`DropDownList<ConsumerConfigAckPolicy>`, defaulted to `Explicit`), Deliver Policy
      (`DropDownList<ConsumerConfigDeliverPolicy>`, defaulted to `All`); constructor takes an
      optional `NewConsumerOptions? initial = null` to pre-fill all fields (used on retry after a
      failed create — Filter Subjects seeds from `string.Join(", ", initial.FilterSubjects)`, same
      as `CreateStreamDialog` already does for its own Subjects field)
- [x] 2.2 Confirm whether `DropDownList<T>` supports a curated (non-exhaustive) source list; if
      so, populate Ack Policy with `Explicit`/`All`/`None` only (excluding `FlowControl`) and
      Deliver Policy with `All`/`Last`/`New`/`LastPerSubject` only (excluding `ByStartSequence`/
      `ByStartTime`). If it only supports enumerating every enum member, fall back to exposing all
      values for that dropdown and note it as a follow-up (see design.md's Risks) rather than
      blocking this task
      — Confirmed via reflection on the installed Terminal.Gui 2.4.10 package:
      `DropDownList<TEnum>` has only a parameterless constructor (auto-populates from every enum
      value) and inherits an untyped `IListDataSource Source` from the non-generic `DropDownList`
      with no documented safe way to substitute a curated subset without risking `Value`'s
      index-based mapping.
      — **Follow-up completed**: rather than exposing all enum values, `CreateConsumerDialog.cs`
      defines `ConsumerCreateAckPolicy` (`Explicit`/`All`/`None`) and
      `ConsumerCreateDeliverPolicy` (`All`/`Last`/`New`/`LastPerSubject`) as the dropdowns' actual
      type parameters, with `ToCurated()`/`ToWire()` converting to/from the real
      `ConsumerConfigAckPolicy`/`ConsumerConfigDeliverPolicy` at the dialog boundary. Both
      dropdowns now only offer the curated subset design.md specifies.
- [x] 2.3 Wire per-field validity (reuse `CreateStreamDialog`'s `SetFieldValidity`/red-text-on-
      `EditableBackground` convention) for Name only (non-empty after trim) — Filter Subjects and
      both dropdowns are always valid. Add a single `Create` button, disabled/inert while Name is
      invalid; no separate `Cancel` button (Esc already cancels via `Dialog<T>`'s built-in
      behavior); override `OnAccepting` to swallow Enter pressed on a field (a no-op, not a submit
      or a cancel) so Tab stays the only way to move between fields
- [x] 2.4 On `Create`, build `Result` as a `NewConsumerOptions` from the four fields' current
      values — Filter Subjects via `NewStreamOptions.ParseSubjects(_filterSubjectsField.Text)` —
      the dialog never constructs or references `ConsumerConfig` directly
- [x] 2.5 Set `Result` and `RequestStop()` on `Create`; leave `Result` unset on Esc

## 3. Wiring into the consumer list and tab

- [x] 3.1 Add `Command.New` + `Key.N.WithCtrl` binding to `Streams/ConsumerListView.cs`
      (alongside its existing `Command.Cancel`/Esc/Backspace wiring for `AscendRequested`),
      raising a `CreateRequested` event — same shape as `StreamListView`'s existing Ctrl+N wiring
- [x] 3.2 In `Streams/StreamsTab.cs`, handle `ConsumerListView.CreateRequested` with a retry loop
      analogous to `TryCreateStreamAsync`: run `CreateConsumerDialog` (seeded from the prior
      attempt's values after a failure, `null` initially) scoped to `_currentStream`; if the user
      cancels (`Result is null`), stop; otherwise try
      `_jetStream.CreateConsumerAsync(_currentStream, result.ToConsumerConfig())`
- [x] 3.3 On success, re-run `RefreshConsumerListAsync()` (extend it to accept an optional
      `selectName` the same way `RefreshListAsync` does) so the new consumer appears and is
      highlighted via `ReplaceItems`'s existing identity-preserving logic, then stop the loop
- [x] 3.4 On `CreateConsumerAsync` failure, catch the exception and call
      `MessageBox.ErrorQuery(App!, "Create Consumer Failed", ex.Message, "_Ok")` — body is exactly
      `ex.Message`, no other text — then loop back to 3.2, reopening `CreateConsumerDialog` seeded
      with the just-entered `NewConsumerOptions`
- [x] 3.5 Add the Ctrl+N shortcut to `ConsumerListView`'s `IShortcutSource.Shortcuts` (append to
      the base `DrillableListView<T>.Shortcuts`, alongside the existing Esc "Back" entry) so it
      surfaces in the status bar
- [x] 3.6 Confirm `CreateRequested` on `ConsumerListView` has no effect while the stream level is
      shown — it shouldn't be reachable at all (the binding lives on `ConsumerListView`, which is
      `Visible = false` at the stream level), but verify no stray focus/key-routing path lets
      Ctrl+N reach it before `Descend()` has run
      — Verified structurally: `_consumerListFrame`/`_consumerListView` start `Visible = false` and
      only flip to `true` inside `Descend()`; `ConsumerListView`'s Esc/Backspace `AscendRequested`
      binding already relies on this exact same visibility gate today (it's never reachable from
      the stream level either), so `Command.New`'s new binding on the same view is gated
      identically — no new focus/key-routing path was introduced.

## 4. Verification

- [x] 4.1 Manual pass via tmux against a real `nats-server`: descend into a stream, Ctrl+N → enter
      only a Name, leave Filter Subjects empty and the dropdowns at their defaults → Create →
      confirm a durable, unfiltered, Explicit/All consumer appears highlighted in the consumer
      list and its detail panel matches
      — Verified on `hello-stream` with `test-consumer-1`: appeared highlighted, detail panel
      showed Filter Subject "(none)", Ack Policy Explicit, Deliver Policy All; confirmed via
      `nats consumer info` as a durable pull consumer.
- [x] 4.2 Manual pass: leave Name empty — confirm Create stays unavailable and the Name field is
      flagged invalid; confirm Ctrl+N cannot create an ephemeral consumer via any field
      combination
      — Verified: with Name empty, both plain Enter (on the Name field) and Enter on the tabbed-to
      Create button left the dialog open and created nothing.
- [x] 4.3 Manual pass: Ctrl+N → enter a Name and a single Filter Subject, change Ack Policy and
      Deliver Policy → Create → confirm the created consumer is durable with the entered filter
      and policies, and `nats consumer info` shows it via the plural multi-subject field (a
      one-element list), not the singular one — confirms `ToConsumerConfig()` never uses the
      singular field even for a single entry
      — Verified on `orders` with `test-consumer-3` (single filter `orders.commands.new`):
      `nats consumer info -j` shows `filter_subjects: ["orders.commands.new"]` and no
      `filter_subject` key at all.
- [x] 4.4 Manual pass: Ctrl+N → enter a Name and multiple Filter Subjects separated by a mix of
      spaces/commas/semicolons → Create → confirm the created consumer filters on all of the
      entered subjects, and `nats consumer info` shows it via the plural multi-subject field
      — Verified on `orders` with `test-consumer-2` (`orders.commands.new, orders.events.created;
      orders.requests.refund`): all three subjects parsed and echoed via `filter_subjects`.
- [x] 4.5 Manual pass: attempt to create a consumer with a durable name that already exists on the
      same stream — confirm a `MessageBox.ErrorQuery` shows the server's error message and, after
      dismissing it, the create-consumer dialog reopens with the previously entered values
      (including multi-subject Filter Subjects, correctly re-joined) still filled in
      — Verified via two real server-side rejections encountered during this pass ("workqueue
      stream requires explicit ack" and "filtered consumer not unique on workqueue stream"): each
      showed a `MessageBox.ErrorQuery` titled "Create Consumer Failed" with the server's exact
      message, and dismissing it reopened `CreateConsumerDialog` with Name, the multi-subject
      Filter Subjects (re-joined with ", "), and the previously chosen Ack/Deliver Policy all
      still filled in.
- [x] 4.6 Confirm via `nats consumer info <stream> <name>` (or the detail panel) that a
      Name-only/empty-Filter-Subjects consumer came through as durable with no filter, not with a
      literal empty-string filter or an unexpected `max_deliver`/`max_ack_pending` value
      — Verified with `test-consumer-1`: `nats consumer info` shows `Max Deliver` and
      `Max Ack Pending` at the server's own sensible defaults (unlimited redelivery, 1000 pending),
      not `0` or an empty-string filter.
- [x] 4.7 Confirm Ctrl+N at the stream level still opens `CreateStreamDialog` (unaffected by this
      change) and has no effect when accidentally pressed before descending
      — Verified: Ctrl+N at the stream level opened the "New Stream" dialog (`CreateStreamDialog`),
      unaffected by this change.
