## Context

The Streams tab's consumer level (`Streams/ConsumerListView.cs`, `ConsumerDetails.cs`) is
currently read-only, per `nats-streams`' "Read-Only List" requirement. This mirrors the situation
`add-stream-create` (archived) resolved at the stream level: `DrillableListView<T>` only wires
Ctrl+R, and the create-modal needs multiple fields of different kinds, which the single-`TextField`
`PatternDialog`/`HeaderDialog` don't cover. `CreateStreamDialog`/`NewStreamOptions` already
established the pattern this change reuses wholesale — this design only works out where the
consumer case differs.

`NATS.Client.JetStream.Models.ConsumerConfig` (already used read-only via `ConsumerDetails.cs`)
has 33 settable properties (`Direct` excluded — its doc comment marks it internal-only, never a
client-facing field). Per the proposal, this slice exposes four: Name, Filter Subjects, Ack
Policy, Deliver Policy (restricted to All/Last/New/LastPerSubject). Everything else — including
`OptStartSeq`/`OptStartTime` (the two Deliver Policy values this slice excludes), `AckWait`,
`MaxDeliver`, `MaxAckPending`, and all push-consumer fields (`DeliverSubject`, `DeliverGroup`,
`IdleHeartbeat`, `FlowControl`, `RateLimitBps`) — is out of scope, the last group permanently
rather than deferred (see Non-Goals).

**Verified via reflection against the installed package (`nats.client.jetstream` 2.8.2,
`NATS.Client.JetStream.dll`) — not assumed:** `ConsumerConfig`'s numeric/duration properties
(`MaxDeliver`, `MaxAckPending`, `AckWait`, `NumReplicas`, `MaxWaiting`, `MaxBatch`, `MaxExpires`,
`MaxBytes`, `RateLimitBps`, ...) are all attributed `[JsonIgnore(Condition =
JsonIgnoreCondition.WhenWritingDefault)]`. A CLR-default value (`0` / `TimeSpan.Zero`) on any of
these is *omitted* from the JSON request entirely, letting the server apply its own default —
this is the opposite of `StreamConfig`'s landmine (`MaxMsgs`/`MaxBytes`/`MaxConsumers`/
`NumReplicas` are `Condition = Never`, i.e. always serialized, so a bare `0` there really does ask
for "admit zero messages"). Concretely: **`ToConsumerConfig()` needs no explicit sentinel
substitution for any field this slice doesn't expose** — a bare `new ConsumerConfig()` with only
Name/DurableName/FilterSubjects/AckPolicy/DeliverPolicy set is already safe to send as-is. This
closes the "sentinel landmine" open item flagged in the proposal.

## Goals / Non-Goals

**Goals:**
- Ctrl+N on the consumer-level list opens a modal that creates a consumer on the currently
  drilled-into stream, and the new consumer appears highlighted in the consumer list afterward.
- Reuse the `CreateStreamDialog`/`NewStreamOptions` pattern (labeled fields, per-field validation,
  single `Create` button, Esc-cancels, reopen-pre-seeded-on-failure) with no new UI pattern.
- `NewConsumerOptions` follows the same "nullable = unset, translator does sentinel work" shape as
  `NewStreamOptions`, even though (per the Context finding) there's no sentinel substitution to do
  in this slice — the seam still matters for when Advanced fields get added later.

**Non-Goals:**
- No Advanced fields (`AckWait`, `MaxDeliver`, `Backoff`, `MaxAckPending`, `ReplayPolicy`,
  `SampleFreq`, `MaxWaiting`/`MaxBatch`/`MaxExpires`/`MaxBytes` pull-tuning, `NumReplicas`,
  `MemStorage`, `InactiveThreshold`, `Description`, `Metadata`, `HeadersOnly`, `PauseUntil`,
  `PriorityGroups`/`PriorityPolicy`/`PinnedTTL`, `OptStartSeq`/`OptStartTime`) — deferred to a
  later change, same shape as `add-stream-create`'s deferred Advanced fields.
- No push-consumer support at all (`DeliverSubject`, `DeliverGroup`, `IdleHeartbeat`,
  `FlowControl`, `RateLimitBps`) — not deferred, excluded: nothing in the app consumes via a push
  subscription today (the live feed is core-NATS subscribe, not JetStream push), so a push
  consumer created here would have no receiver. `DeliverSubject` is never set, meaning every
  consumer this dialog creates is a pull consumer.
- No consumer Edit or Delete — only Create is added; the consumer level keeps its "no edit/delete"
  guarantee (narrowed from the current "no mutation" guarantee, which is retiring — see the specs
  delta).
- No ephemeral consumer creation — Name is required, not optional (see Decisions). Ephemeral
  support is a candidate for a future change once `InactiveThreshold` (or equivalent) is exposed,
  not a simplification of this one.
- No NATS-CLI-style shorthand parsing — plain enum dropdowns and free-text fields only, same
  scope decision `add-stream-create` made for its own text fields.

## Decisions

**`NewConsumerOptions`: a dialog-owned result type, separate from `ConsumerConfig`.**

```csharp
internal sealed record NewConsumerOptions(
    string Name,
    IReadOnlyList<string> FilterSubjects,
    ConsumerConfigAckPolicy AckPolicy,
    ConsumerConfigDeliverPolicy DeliverPolicy);
```

`Name` is a required, non-empty `string` — **every consumer this dialog creates is durable, never
ephemeral.** An ephemeral consumer's name is server-generated, so a user creating one through this
admin UI would have no way to hand that name to whatever external process is supposed to attach to
it; worse, ephemeral consumers are auto-deleted by the server after `InactiveThreshold` of no
active pull/subscribe activity, and `InactiveThreshold` is an Advanced field this slice doesn't
expose — so an ephemeral consumer created here would very likely vanish before anything could ever
use it. Ephemeral creation only becomes a sensible option once a future Advanced-fields change
exposes `InactiveThreshold` (or the dialog surfaces the server-assigned name clearly enough to act
on immediately) — until then it's excluded outright, not just deferred.

`FilterSubjects` is `IReadOnlyList<string>` (can be empty — that means "no filter," all stream
subjects), parsed from one delimiter-separated `TextField` the same way `NewStreamOptions.Subjects`
already is — **reuses `NewStreamOptions.ParseSubjects(string)` directly** rather than duplicating
the space/comma/semicolon split, since both types already live in `Streams/` and the parsing rule
(trim, split, drop empty tokens) has no reason to diverge between the two dialogs. Unlike
`NewStreamOptions.Subjects`, an empty result is valid here (a stream needs ≥1 subject to exist
meaningfully; a consumer with zero filters is just "receive everything," a normal and common
choice). `AckPolicy`/`DeliverPolicy` are plain non-nullable enums with defaults (`Explicit`/`All`),
same as `NewStreamOptions.Retention`.

`ToConsumerConfig()` (co-located, same file) is the single place that translates
`NewConsumerOptions` into `ConsumerConfig`. It always writes through the plural `FilterSubjects`
field, never the singular `FilterSubject` — even for exactly one entry:

```csharp
public ConsumerConfig ToConsumerConfig() =>
    new ConsumerConfig(Name.Trim()) with {
        FilterSubjects = FilterSubjects.Count > 0 ? FilterSubjects : null,
        AckPolicy = AckPolicy,
        DeliverPolicy = DeliverPolicy,
    };
```

`K4os.NatsTransit` (`D:\Projects\k4os.natstransit\src\K4os.NatsTransit\Configuration\
NatsConfigurator.cs:66-82`) — a real, independent NATS.Client.JetStream consumer, not this repo's
own code — does exactly this: every consumer it creates sets `FilterSubjects` (plural) regardless
of whether it's filtering on one subject or several, and never touches the singular field at all.
That's the precedent this decision follows instead of the earlier plan (see the "can a consumer
have only one filter subject" discussion) of branching on count to choose singular vs plural on
write — the branch added complexity the server doesn't require and the ecosystem doesn't bother
with. **This only simplifies the write side.** `ConsumerDetails.cs`'s existing *read*-side code
still has to keep unioning both `FilterSubject` and `FilterSubjects` when displaying a consumer
(per its own comment: the server is inconsistent about which one it echoes back, and consumers not
created by this dialog — via `nats consumer add`, another client, or a future edit feature — may
still have the singular field populated). Nothing about the read side changes in this slice; the
simplification is purely "this dialog's own `ToConsumerConfig()` never emits the singular field."

The `ConsumerConfig(string name)` ctor overload is always used (never the parameterless one) since
Name is guaranteed non-empty by dialog validation before `Result` is ever set; per its own doc
comment it sets `Name`/`DurableName` together and defaults `AckPolicy` to `Explicit`, which the
`with` above immediately overwrites — its only surviving effect is "set Name and DurableName
together." Per the Context finding, no other field needs an explicit default.

**Field → widget → parse mapping:**

| Field | Widget | Parse | Validity |
|---|---|---|---|
| Name | `TextField` | trim | non-empty after trim — same rule as `NewStreamOptions.Name`, for the same UI treatment (`SetFieldValidity`, red text when invalid) |
| Filter Subjects | `TextField` | `NewStreamOptions.ParseSubjects(string)` — space/comma/semicolon-delimited, trimmed, empty tokens dropped | always valid — zero results means "no filter," not an error, unlike Streams' own Subjects field |
| Ack Policy | `DropDownList<ConsumerConfigAckPolicy>` | — | always valid (default `Explicit`) — `FlowControl` is excluded from the dropdown's populated values (internal/mirror-source use per its own doc comment, not a user-facing choice) |
| Deliver Policy | `DropDownList<ConsumerConfigDeliverPolicy>` | — | always valid (default `All`) — populated with only `All`/`Last`/`New`/`LastPerSubject`; `ByStartSequence`/`ByStartTime` are not offered in this dialog at all (they need a companion value field this slice doesn't have a UI pattern for) |

Name is the one field that can be invalid, exactly mirroring `CreateStreamDialog`'s own Name field
(`UpdateValidity()`/`SetFieldValidity` port over unchanged, just for a single field instead of
three) — `Create` stays disabled while Name is empty/whitespace-only. Filter Subjects and both
dropdowns are always valid — Filter Subjects parses with the same helper as Streams' Subjects
field but, unlike that field, an empty parse result isn't rejected.

`DropDownList<TEnum>` is used for both enum fields, matching `CreateStreamDialog`'s
`DropDownList<StreamConfigRetention>` precedent (compact footprint, `IValue<TEnum?>`). Populating
a dropdown with a *subset* of an enum's values (excluding `FlowControl` from Ack Policy,
`ByStartSequence`/`ByStartTime` from Deliver Policy) needs confirming `DropDownList<T>` supports
an explicit source-list constructor rather than always enumerating every enum member — check
during implementation; if it only supports "every enum value," the two excluded values get
flagged as an implementation-time follow-up rather than blocking this design.

**Resolved during implementation:** `DropDownList<TEnum>` (Terminal.Gui 2.4.10, confirmed via
reflection) has no curated-subset constructor — `Source` is inherited untyped from the
non-generic base with no documented safe way to substitute a subset without risking `Value`'s
index-based mapping. Rather than exposing all enum values (the fallback this design originally
accepted), `CreateConsumerDialog.cs` defines two dialog-private enums —
`ConsumerCreateAckPolicy` (`Explicit`/`All`/`None`) and `ConsumerCreateDeliverPolicy`
(`All`/`Last`/`New`/`LastPerSubject`) — as the dropdowns' actual type parameters, each a curated
subset of the corresponding `ConsumerConfig*` enum. `ToCurated()`/`ToWire()` convert at the
dialog boundary (`ToCurated` when seeding a dropdown from a retry's `initial`, `ToWire` when
building `Result` in `Commit()`), so `NewConsumerOptions`/`ToConsumerConfig()` still deal only in
the real wire enums — nothing outside `CreateConsumerDialog.cs` ever sees the curated types.

**Dialog shape: `Dialog<NewConsumerOptions>`, single `Create` button, Esc cancels.** Identical
shape to `CreateStreamDialog` — `Create` is reached via Tab+Enter or click, plain Enter on a field
is a swallowed no-op (`OnAccepting` override), Esc cancels via `Dialog<T>`'s own default. No
`Cancel` button, same mnemonic-collision reasoning as `CreateStreamDialog`.

**Ctrl+N lives on `ConsumerListView`, mirroring `StreamListView`.** `ConsumerListView` currently
only layers Esc/Backspace (`AscendRequested`) on top of `DrillableListView<T>`'s Refresh-only
base; this adds a `Command.New` + `Key.N.WithCtrl` binding and a `CreateRequested` event, the same
shape `StreamListView` already uses for its own `CreateRequested`. `StreamsTab` handles it:
because `_currentStream` already holds the drilled-into stream's name (set on `Descend()`,
cleared on `Ascend()`), `OpenCreateConsumerDialog` needs no new state — it just closes over
`_currentStream` at call time. `StreamsTab` calls
`_jetStream.CreateConsumerAsync(_currentStream, options.ToConsumerConfig())`, and on success
`_consumerListView.ReplaceItems(...)` with the refreshed list and the new consumer's name — same
identity-preserving-highlight mechanics `TryCreateStreamAsync` already relies on.

**Errors surface via a modal `MessageBox.ErrorQuery`, reopening the dialog pre-seeded.** Identical
to `TryCreateStreamAsync`'s error path: catch the exception, show
`MessageBox.ErrorQuery(App!, "Create Consumer Failed", ex.Message, "_Ok")`, then reopen
`CreateConsumerDialog` seeded with the just-entered `NewConsumerOptions` so nothing typed is lost.

## Risks / Trade-offs

- **[Risk]** Restricting the Deliver Policy dropdown to 4 of 6 enum values means a user who wants
  "start from sequence N" or "start from time T" has no path in this dialog at all, not even an
  awkward one → **Mitigation**: explicitly scoped out per the proposal; those two values are
  exactly the Advanced-fields seed for a follow-up change (they'd need a companion
  `OptStartSeq`/`OptStartTime` field, which is new UI shape, not just a new dropdown entry).
- **[Risk]** `DropDownList<TEnum>` may not support a curated subset of enum values out of the box
  (see Decisions) → **Mitigation**: confirm during implementation; worst case, populate the
  dropdown with all 6/5 values and treat "user picks an excluded value" as a solvable follow-up
  rather than blocking this change — but check first, since `CreateStreamDialog`'s
  `DropDownList<StreamConfigRetention>` precedent didn't need to curate (all `StreamConfigRetention`
  values are exposed there).
  — **Resolved**: confirmed no curated-subset constructor exists; rather than accept the "expose
  all values" fallback, `CreateConsumerDialog.cs` introduces its own curated
  `ConsumerCreateAckPolicy`/`ConsumerCreateDeliverPolicy` enums as the dropdowns' type parameters,
  converting to/from the real `ConsumerConfig*` enums at the dialog boundary (see Decisions). Both
  dropdowns now only ever offer the intended subset.
- **[Risk]** No client-side validation exists for Name's character set or individual Filter
  Subjects' subject syntax → **Mitigation**: same accepted trade-off as `add-stream-create`'s Name
  and Subjects fields — failures surface via `MessageBox.ErrorQuery` and the dialog reopens
  pre-seeded for a quick fix.
- **[Risk]** The proposal narrows `nats-streams`' "Read-Only List" requirement's consumer-level
  guarantee — any other code or test relying on the old, stricter "no mutation affordance"
  wording needs to be found → **Mitigation**: grep for the current requirement text before
  archiving; per the Impact section, no other capability references it.

## Migration Plan

No data migration. Purely additive UI/behavior; existing streams, consumers, and the stream-level
Create/Delete behavior are unaffected. No feature flag — ships as soon as merged.

## Open Questions

- ~~Whether `DropDownList<T>` supports a curated (non-exhaustive) value list~~ — **Resolved**: it
  doesn't natively, but a dedicated curated enum per dropdown (converted at the dialog boundary)
  gets the same effect without a follow-up validation guard or a different widget — see Decisions
  and Risks.
- Whether individual Filter Subjects get any client-side subject-syntax validation now or in a
  follow-up — leaning follow-up, consistent with Name's char-set question staying open in
  `add-stream-create`.
