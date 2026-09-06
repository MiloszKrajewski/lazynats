## Context

Four read-only detail panels (`StreamDetails`, `ConsumerDetails`, KV's `BucketDetails`, OBJ's
`BucketDetails`) build their rows as `(string Label, string Value)[]` from JetStream/KV/OBJ config
objects, mostly via bare `.ToString()`. Several of those config fields use NATS server sentinel
values to mean "no limit," and those sentinels differ by field *type*, not just by field:

| Field (panel)                                  | Type       | Sentinel        | Meaning     |
|-------------------------------------------------|------------|-----------------|-------------|
| Stream `MaxMsgs` / `MaxBytes`                    | `long`     | `-1`            | unlimited   |
| Stream `MaxAge`                                  | `TimeSpan` | `TimeSpan.Zero` | unlimited   |
| Consumer `MaxDeliver` / `MaxAckPending`          | `long`     | `-1`            | unlimited   |
| KV `MaxMsgsPerSubject` ("History")               | `long`     | `-1`            | unlimited   |
| KV `MaxAge`, OBJ `MaxAge`                        | `TimeSpan` | `TimeSpan.Zero` | unlimited   |

This is consistent with the NATS JetStream wire API: every `Max*` count field on `StreamConfig`
and `ConsumerConfig` uses `-1` for "no limit" (confirmed for `MaxMsgs`/`MaxBytes`/`MaxConsumers`/
`MaxMsgSize` in the shipped `NATS.Client.JetStream.xml` doc comments, and for `MaxDeliver`/
`MaxAckPending` via NATS server source/docs, since the client library's own doc comments don't
spell it out for those two), while every `Max*` duration field (`MaxAge`) uses the zero value.
KV's bucket config is a `StreamConfig` under the hood (`NatsKVStatus.Info` *is* a `StreamInfo`,
per the existing comment in `Kv/BucketDetails.cs`), so its `MaxAge` and `MaxMsgsPerSubject` follow
the same rule.

`Kv/BucketDetails.cs` already gets this right for one field — `TTL Limit` — via a local ternary
(`status.LimitMarkerTTL > TimeSpan.Zero ? ... : "(none)"`). That field is semantically different
(an optional feature that's either configured or absent, not a capped-vs-uncapped limit), so it
keeps its own "(none)" wording; it's the template for *how* to intercept a sentinel, not a
value or wording to reuse for the unlimited case.

## Goals / Non-Goals

**Goals:**
- Every currently-displayed limit field that carries an "unlimited" sentinel renders as
  `(unlimited)` instead of the raw server value, across all four detail panels.
- One shared place defines the count-sentinel and duration-sentinel checks, so the mapping isn't
  copy-pasted six times with room to drift.

**Non-Goals:**
- No new rows are added to any detail panel (e.g. not surfacing `DuplicateWindow`,
  `MaxConsumers`, or `MaxMsgSize`, which aren't shown today). Scope is reformatting what's already
  there.
- No `(disabled)` case: none of the currently-displayed fields have a disabled-style sentinel: The
  candidates the proposal's wording gestures at (`(unset)`, `(disabled)`) don't have a concrete
  instance among today's displayed fields — `TTL Limit`'s existing `(none)` already covers the
  one "(unset)"-shaped case. The helper is still written as two narrowly-named methods rather than
  one that also takes a label string, so a future `(disabled)`-style field is a new small method,
  not a speculative parameter added now for a case that doesn't exist yet.
- No change to create/edit dialogs (`CreateStreamDialog`, `CreateBucketDialog`, etc.) — those
  already document "empty means unlimited" input-side behavior in their own specs; this change is
  read-only-panel output formatting.

## Decisions

**Shared helper: `Components/LimitFormat.cs`, two static methods.**
```csharp
internal static class LimitFormat
{
    public static string Count(long value) => value < 0 ? "(unlimited)" : value.ToString();
    public static string Duration(TimeSpan value) => value <= TimeSpan.Zero ? "(unlimited)" : value.ToString();
}
```
- Lives in `Components/` alongside `DialogText.cs` (an existing small stateless string-formatting
  static class), not in `KVStore/`, `Streams/`, or `ObjStore/`, since it's shared across all three
  areas — mirrors where `PollingDetailsView` itself (the base class all four panels build on)
  already lives.
- `Count` treats *any* negative value as unlimited, not just `-1`. The server only ever sends
  `-1`, but the check costs nothing and avoids a false "raw number" leaking through if that ever
  changes.
- `Duration` uses `<=` (matching the existing `TTL Limit` ternary's `>` check inverted), not `==`,
  for the same reason.
- Two small methods rather than one generic `Format<T>` or a method taking a label parameter:
  the two call shapes (`long` vs `TimeSpan`) are the only ones needed, and every call site wants
  literally `(unlimited)` — no field currently needs a caller-supplied label.

**Alternative considered: inline ternaries at each call site (status quo pattern for `TTL
Limit`).** Rejected — six call sites means six chances for the sentinel check to be copy-pasted
wrong (e.g. `< 0` vs `<= 0`, or `TimeSpan.Zero` vs `default`), and a reviewer can't tell at a
glance whether two inline ternaries encode the same rule.

**Alternative considered: format at the `NatsKVStatus`/`StreamInfo` boundary (a wrapping
DTO).** Rejected — these are library-owned model types (`NATS.Client.JetStream.Models.*`), the
existing pattern is entirely `BuildRows`-local formatting, and a wrapping DTO is more machinery
than four `BuildRows` methods calling a two-method helper.

## Risks / Trade-offs

- [Getting a sentinel wrong for a field where the shipped XML doc doesn't state one explicitly
  (`ConsumerConfig.MaxDeliver`, `MaxAckPending`) — the NATS.Client.JetStream.xml doc comments only
  spell out `-1 for unlimited` for `StreamConfig`'s count fields, not `ConsumerConfig`'s] →
  Verified against NATS server source/docs during research for this change (both are `-1`); the
  implementation task list includes a manual check against a real consumer created with explicit
  `MaxDeliver`/`MaxAckPending` unlimited settings (e.g. via `nats consumer add` / the `nats` CLI
  in `.bin/`) to confirm the client library surfaces the same `-1` rather than silently defaulting
  it server-side.
- [`(unlimited)` could be mistaken for "not configured" rather than "no cap"] → Accepted; this
  matches NATS's own terminology (the `nats` CLI itself prints "unlimited" for these same
  sentinels), so it's the least surprising wording for anyone already familiar with NATS tooling.

## Migration Plan

Not applicable — local formatting change, no data migration, no server interaction change, no
backward-compatibility surface.
