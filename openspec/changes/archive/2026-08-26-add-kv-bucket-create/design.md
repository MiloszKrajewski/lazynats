## Context

The KV tab (`KVStore/KvTab.cs`, `BucketListView.cs`, `BucketDetails.cs`) is currently read-only,
per `nats-kv`'s "Read-Only Tab" requirement. `BucketListView` extends
`Components/DrillableListView<T>`, which only wires Ctrl+R (Refresh) — the same starting point
`StreamListView` had before `add-stream-create` layered its own Ctrl+N on top. That change is the
direct precedent here: same base class, same multi-field-dialog problem (`PatternDialog`/
`HeaderDialog` are single-`TextField` `Dialog<string>` and don't fit), same
"CLR-default-sentinel-on-the-wire-type" landmine.

`NATS.Client.KeyValueStore.NatsKVConfig` (installed version 2.8.2, already used read-only via
`BucketDetails.cs`) has a dozen-plus settable properties. Per the proposal, this slice exposes
Name, Storage, History, Max Age, and Limit Marker TTL; everything else (`MaxValueSize`,
`MaxBytes`, `NumberOfReplicas`, `Compression`, `Republish`, `Placement`, `Mirror`, `Sources`,
`Metadata`) is left for a future "Advanced" section, created with a safe explicit default rather
than a user-facing field. Storage rides along with the essential fields rather than Advanced
because it's a one-time, lifecycle-fixing choice — `NatsKVContext`'s own `UpdateStoreAsync` doc
states "Storage type cannot change" — and Memory-backed buckets are a distinct, common use case
(fast ephemeral caches) rather than a rarely tuned knob; deferring it would force cache-style
buckets through the `nats` CLI regardless of what else this dialog can do. Limit Marker TTL
rides along with Max Age rather than waiting for Advanced because the two are coupled from the
user's point of view: with Max Age left unlimited (this slice's default), per-key TTL deletion is
the only expiry path a bucket has, and `LimitMarkerTTL` is what decides whether that deletion
leaves a visible tombstone (`BucketDetails.cs` already surfaces the created value back as its
"TTL Limit" row) or vanishes without a trace for other clients watching the bucket. Deferring it
to Advanced would ship a bucket-creation dialog that can produce unlimited-Max-Age buckets with
no way to control that tradeoff.

History was originally scoped out of this slice's dialog — a plain KV bucket keeping only its
latest revision per key is the common case, and unlike Storage it isn't fixed for the bucket's
lifetime (`NatsKVContext.UpdateStoreAsync`'s own doc places no such restriction on History), so
there seemed to be no urgency to expose it before Advanced exists. That was revisited: a
multi-revision bucket (an audit trail, a small event log keyed by ID) is a distinct, common use
case in the same way a Memory-backed bucket is, and forcing that choice through the `nats` CLI
just because this dialog stopped one field short of covering it defeats the point of closing the
CLI-round-trip gap at all — so History joins Storage as a field decided at creation time rather
than deferred, even though (unlike Storage) it remains editable later once an Edit/Advanced
surface exists. A KV bucket is a JetStream stream under the hood (`BucketName.cs`,
`BucketDetails.cs` both lean on this), and `NatsKVConfig`'s `History` property is documented as
"Maximum historical entries" — `BucketDetails.BuildRows` already reads this same concept back off
the created stream as `config.MaxMsgsPerSubject`. As with `StreamConfig.MaxMsgs`/`MaxBytes` in
`add-stream-create`, a CLR-default `NatsKVConfig` left untouched on `History` risks landing on
`0`, which for a "how many revisions to keep per key" field means "keep none" — not a usable
bucket; unlike `MaxAge`/`LimitMarkerTTL` (whose CLR-default `TimeSpan.Zero` is coincidentally
their own genuine "unlimited"/"disabled" sentinel), History's CLR default of `0` isn't a
meaningful "unset" value, so an unset History field defaults to `1` (keep only the latest
revision per key) in `ToNatsKVConfig()` rather than passing the CLR default straight through —
this design keeps the dialog's result type separate from `NatsKVConfig` for exactly this reason,
the same one `add-stream-create` had: `null` should mean "the user didn't set this", and the
reader shouldn't have to know which of `NatsKVConfig`'s own CLR defaults happen to be safe.

## Goals / Non-Goals

**Goals:**
- Ctrl+N on the bucket-level list opens a modal that creates a bucket with five essential
  fields (Name, Storage, History, Max Age, Limit Marker TTL), and the new bucket appears
  highlighted in the list afterward.
- Reuse the multi-field-dialog pattern `add-stream-create` established (labeled fields, per-field
  validation, a single `Create` button, Esc-cancels, reopen-seeded-on-failure) rather than
  inventing a new one.
- Keep field parsing (History count, Max Age duration, Limit Marker TTL duration) in dedicated,
  individually testable methods, matching `CreateStreamDialog`'s `TryParseMaxAge` precedent, so a
  later Advanced-fields change or a friendlier duration syntax can replace them without touching
  dialog wiring.

**Non-Goals:**
- No Advanced fields (Max Value Size, Max Bytes, Replicas, Compression, Republish, Placement,
  Mirror/Sources, Metadata, ...) in this slice — deferred, per the proposal, to a later change.
- No bucket Edit or Delete, and no key-level mutation of any kind — `nats-kv`'s key-level
  read-only guarantee stays true, and only bucket-level Create is added (bucket-level
  Edit/Delete remain out of scope, mirroring how `add-stream-create` left stream Edit/Delete out).
- No NATS-CLI-style shorthand duration parsing (`7d`, `10MB`) — `TimeSpan.Parse`/`int.Parse`
  only, matching `add-stream-create`'s explicit scope decision for the same fields.

## Decisions

**`NewBucketOptions`: a dialog-owned result type, separate from `NatsKVConfig`.** The dialog
returns `KVStore/NewBucketOptions.cs`, a plain record shaped around user intent:

```csharp
internal sealed record NewBucketOptions(
    string Name,
    NatsKVStorageType Storage,
    int? History,
    TimeSpan? MaxAge,
    TimeSpan? LimitMarkerTTL);
```

`Storage` is non-nullable — unlike History/Max Age/Limit Marker TTL, it always has a concrete
selection (the dropdown defaults to `File`, matching `NatsKVConfig.Storage`'s own documented
default), so there's no "unset" state to represent. `History`, `MaxAge`, and `LimitMarkerTTL`
stay nullable — `null` means "the user left it unset", not a magic `0`/`TimeSpan.Zero` the reader
has to know is special (`History`'s own CLR default isn't itself a safe "unset" value the way
`MaxAge`/`LimitMarkerTTL`'s `TimeSpan.Zero` coincidentally is — see the CLR-default discussion
below). This is the same seam `NewStreamOptions.MaxAge` established, and it's where the future
Advanced page's additional fields (`MaxValueSize`, `NumberOfReplicas`, ...) join as their own
nullable CLR-natural types once exposed.

A separate `ToNatsKVConfig()` conversion (co-located in the same file) is the single place that
translates user intent into `NatsKVConfig`'s wire shape, including the explicit-default
substitution described next. `KvTab` calls `dialogResult.ToNatsKVConfig()` immediately before
`_kv.CreateStoreAsync(...)` — the dialog itself never constructs or references `NatsKVConfig`.

**Field → widget → parse mapping (dialog side, in terms of `NewBucketOptions`):**

| Field | Widget | Parse | Validity |
|---|---|---|---|
| Name | `TextField` | — | non-empty after trim |
| Storage | `DropDownList<NatsKVStorageType>` | — | always valid (has a default selection, `File`) |
| History | `TextField` | `TryParseHistory(string, out int? result)` — empty means null (unset, defaults to `1`); non-empty must parse as an `int` and be `>= 1` (a "keep 0 revisions" bucket isn't usable, matching the CLR-default landmine this field would otherwise be) | field is valid when `TryParseHistory` returns `true` |
| Max Age | `TextField` | `TryParseMaxAge(string, out TimeSpan? result)` — same shape as `CreateStreamDialog.TryParseMaxAge`, reimplemented here rather than shared (see below) | field is valid when `TryParseMaxAge` returns `true` |
| Limit Marker TTL | `TextField` | `TryParseLimitMarkerTTL(string, out TimeSpan? result)` — same empty-means-null/`TimeSpan.Parse`-otherwise shape as `TryParseMaxAge` | field is valid when `TryParseLimitMarkerTTL` returns `true` |

`Storage` reuses `CreateStreamDialog`'s `DropDownList<StreamConfigRetention>` precedent —
`DropDownList<NatsKVStorageType>`, same reasoning (compact single-line footprint, room left for
Advanced fields later) — so this dialog ends up the same shape as `CreateStreamDialog`: one
dropdown plus several `TextField`s and the `Create` button.

**`TryParseMaxAge`-shaped parsing is reimplemented in `KVStore`, not shared with `Streams`, and
`TryParseLimitMarkerTTL` reuses the same shape rather than delegating to `TryParseMaxAge`.**
`Streams/CreateStreamDialog` and `KVStore/CreateBucketDialog` both need "empty string means null,
otherwise parse a `TimeSpan` or fail", but there's no existing shared home for
dialog-field-parsing helpers, and the call sites are small enough that extracting a shared helper
now would be speculative — `NewBucketOptions.cs` gets its own `TryParseMaxAge`, same as
`NewStreamOptions.cs`'s. `TryParseLimitMarkerTTL` is a second, separate method with identical
logic rather than `TryParseMaxAge` called under a different name — the two fields are
conceptually distinct (one bounds entry age, the other bounds tombstone retention) even though
today's parsing happens to coincide, and a future change to one's parsing rules (e.g. Max Age
gaining `7d`-style shorthand first) shouldn't silently move the other's. If a third `TimeSpan`
field needs the same parsing, that's the trigger to extract a shared `Components`-level helper;
not before. `TryParseHistory` is a fourth, unrelated method (parses `int`, not `TimeSpan`) with
the same empty-means-null shape for consistency, not because it shares logic with the other two.

**`Storage` passes straight through — no CLR-default concern.** Unlike `History` (see below),
`Storage` is non-nullable on `NewBucketOptions` and always carries an explicit user (or
dialog-default) selection, so `ToNatsKVConfig()` just assigns `Storage = Storage` with no
substitution logic — there's no "unset" state that could silently resolve to something unusable
the way a bare `0` on `History`/`NumberOfReplicas` would.

**`History` is nullable on `NewBucketOptions`, unlike `Storage` — its CLR default isn't a safe
"unset" value, so `ToNatsKVConfig()` substitutes one.** `History` is now a dialog field (a pivot
from this change's original scope, which hardcoded it — see Context), but its CLR default of `0`
is still not a meaningful "keep zero revisions" request the way `MaxAge`/`LimitMarkerTTL`'s CLR
default of `TimeSpan.Zero` coincidentally is their own genuine "unlimited"/"disabled" sentinel.
`ToNatsKVConfig()` therefore maps `History ?? 1` (keep only the latest revision per key when
unset, matching what `nats kv add` defaults to and what `BucketDetails`'s "History" row already
expects to see as a small positive number, not zero) — the same "null means unset, substitute a
safe default" shape as `LimitMarkerTTL ?? TimeSpan.Zero` below, just with a non-zero substitute
because History's CLR default isn't itself safe. Client-side validity additionally requires a
non-empty History field to be `>= 1` (see the field/widget/parse table above), so `ToNatsKVConfig()`
never sees a user-entered `0` to worry about — only the true "left unset" `null` case.

**Explicit safe defaults for `NatsKVConfig` fields `NewBucketOptions` doesn't carry at all.** A
bare `new NatsKVConfig(name)` leaves `MaxValueSize`/`MaxBytes`/`NumberOfReplicas` at the CLR
default of `0`. `ToNatsKVConfig()` unconditionally sets `NumberOfReplicas = 1` (a bucket with
zero replicas isn't a meaningful request either). `MaxValueSize`/`MaxBytes` are left at their CLR
defaults for this slice: unlike `StreamConfig.MaxMsgs`/`MaxBytes` (confirmed `-1`-means-unlimited
via existing doc comments), `NatsKVConfig`'s size-limit fields' zero-vs-unlimited semantics
aren't yet confirmed against the installed 2.8.2 client — flagged as an open question, not
guessed at.

An unset `LimitMarkerTTL` is left at `TimeSpan.Zero` — its own doc comment states `0` means
"markers are not supported", which is a legitimate, safe default (TTL-marker retention is an
opt-in feature, not something every bucket needs), not a CLR-default landmine the way `History`
left untouched would be. `ToNatsKVConfig()` therefore maps `LimitMarkerTTL ?? TimeSpan.Zero` with
no special-casing beyond that.

**Dialog shape: `Dialog<NewBucketOptions>` with a single `Create` button; Esc cancels.** Same
shape as `CreateStreamDialog`: Tab moves between fields, Enter on a field is swallowed via
`OnAccepting` rather than closing the dialog, `Create` is disabled while any field is invalid, no
separate `Cancel` button (Esc already cancels via `Dialog<T>`'s built-in behavior).

**Ctrl+N lives on `BucketListView`, not the shared base.** `DrillableListView<T>` stays
Refresh-only (it's also the base for `KeyListView`, which must remain create-less per `nats-kv`'s
key-level read-only guarantee). `BucketListView` adds its own `Command.New` + `Key.N.WithCtrl`
binding, the same way `StreamListView` layers its own Ctrl+N on the shared base, and raises a
`CreateRequested` event that `KvTab` handles: run the dialog, translate via `ToNatsKVConfig()`,
call `CreateStoreAsync`, and on success re-run `RefreshListAsync()` so `ReplaceItems`'s existing
identity-preserving logic highlights the new bucket.

**Errors surface via a modal `MessageBox.ErrorQuery`, not `StatusChanged`.** Same rationale as
`add-stream-create`: `StatusChanged` is for passive, transient poll/refresh failures; a failed
`CreateStoreAsync` is the direct result of an explicit user action and deserves a blocking
acknowledgment. `KvTab` catches the exception, calls
`MessageBox.ErrorQuery(App!, "Create Bucket Failed", ex.Message, "_Ok")` (body is exactly
`ex.Message`), and on dismissal reopens `CreateBucketDialog` seeded with the just-entered
`NewBucketOptions` so nothing typed is lost.

## Risks / Trade-offs

- **[Risk]** `NatsKVConfig.MaxValueSize`/`MaxBytes` zero-vs-unlimited semantics aren't confirmed
  against the installed client version, and this slice leaves them untouched → **Mitigation**:
  both fields are outside this slice's exposed surface (Advanced-only), so any landmine there is
  inherited unchanged from server defaults, not introduced by this change; confirm during
  implementation (task list includes a verification step) and note the finding in code if the
  CLR default turns out to be unsafe.
- **[Risk]** `History`'s unset-default of `1` is an assumption about NATS KV convention (matching
  `nats kv add`'s own default), not a value read from the installed client → **Mitigation**:
  verified with a manual create-and-inspect pass leaving History unset (task list includes this);
  the comment at the `?? 1` substitution site records the assumption so it's easy to correct if
  wrong. Once History is user-settable, this only matters for the unset case — an explicit value
  round-trips as entered, verified separately.
- **[Risk]** No server-side validation of a user-entered History value beyond client-side `>= 1`,
  so an unreasonably large value (server-side limits, if any, aren't known) only fails after the
  round-trip → **Mitigation**: acceptable for this slice, same reasoning as the bucket-name risk
  below — the failure surfaces via `MessageBox.ErrorQuery` and the dialog reopens pre-seeded.
- **[Risk]** `TryParseMaxAge` duplicated across `Streams` and `KVStore` rather than shared →
  **Mitigation**: accepted per the "no speculative extraction" decision above; both copies are a
  few lines and trivially keep in sync by inspection if one changes.
- **[Risk]** No server-side bucket-name validation happens client-side beyond non-empty, so
  invalid names (NATS restricts bucket names to a limited character set, same as stream names)
  only fail after the round-trip → **Mitigation**: acceptable for this slice, same reasoning
  `add-stream-create` used — the failure surfaces via `MessageBox.ErrorQuery` and the dialog
  reopens pre-seeded for a quick fix.
- **[Risk]** `LimitMarkerTTL`'s doc comment notes the feature "is only available on NATS server
  v2.11 and later" — setting a non-zero value against an older server is a request the server may
  reject or silently ignore, and this slice doesn't detect server version client-side →
  **Mitigation**: leaving the field empty (the default) sends `TimeSpan.Zero`, which is a no-op
  on any server version, so the risk only materializes when a user explicitly sets a value; a
  rejection surfaces through the same `MessageBox.ErrorQuery` path as any other create failure,
  and silent-ignore (if that's the server's actual behavior on old versions) is a follow-up
  concern, not a blocker for this slice.

## Migration Plan

No data migration. Purely additive UI/behavior; existing buckets and the key-level read-only
behavior are unaffected. No feature flag — ships as soon as merged.

## Open Questions

- ~~Confirm `NatsKVConfig`'s CLR-default `History` (0) is not silently coerced to a usable value
  by the client/server before assuming an explicit substitute value is necessary for the unset
  case~~ — **Resolved during implementation** (prior to the History-field pivot, still valid
  after it): verified live against a server (v2.12.8) via `nats kv info` on a bucket created
  through this dialog with History left unset — `History Kept: 1`, not silently coerced from any
  CLR default. The `History ?? 1` substitution in `ToNatsKVConfig()` is necessary, as assumed.
- ~~Confirm an explicit, user-entered History value (not just the unset-default `1`) round-trips
  correctly through `CreateStoreAsync`~~ — **Resolved during implementation of the History-field
  pivot**: verified live against a server (v2.12.8) — a bucket created with History `3` showed
  `History Kept: 3` via `nats kv info`, matching the entered value exactly.
