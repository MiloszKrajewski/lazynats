## Context

The OBJ tab (`ObjStore/ObjTab.cs`, `BucketListView.cs`, `BucketDetails.cs`) is currently
read-only, per `nats-obj`'s "Read-Only Tab" requirement. `BucketListView` extends
`Components/DrillableListView<T>`, which only wires Ctrl+R (Refresh). `add-kv-bucket-create` is
the direct precedent here — same base class, same multi-field-dialog problem
(`PatternDialog`/`HeaderDialog` are single-`TextField` `Dialog<string>` and don't fit), same
"CLR-default-sentinel-on-the-wire-type" landmine — applied to a different store.

`NATS.Client.ObjectStore.NatsObjConfig` (installed version 2.8.2, already used read-only via
`BucketDetails.cs`) has: `Bucket`, `Description`, `MaxAge`, `MaxBytes`, `Storage`,
`NumberOfReplicas`, `Placement`, `Metadata`, `Compression`. This is a smaller surface than
`NatsKVConfig`'s — Object Store has no per-key revisioning, so there's no analog of KV's
`History` or `LimitMarkerTTL`. Per the proposal, this slice exposes only Name and Max Age;
everything else (`Description`, `MaxBytes`, `Storage`, `NumberOfReplicas`, `Placement`,
`Metadata`, `Compression`) is left with a safe explicit default rather than a user-facing field.
This is a narrower core set than `add-kv-bucket-create` chose for KV: that dialog kept `Storage`
alongside Name/History/MaxAge/LimitMarkerTTL because a memory-backed *KV* bucket is a common,
deliberate choice (a fast ephemeral cache); a memory-backed *Object Store* bucket is a rare
choice in practice, so `Storage` joins the same deferred-to-Advanced bucket as the size/replica/
compression/placement/metadata fields `add-kv-bucket-create` already deferred.

## Goals / Non-Goals

**Goals:**
- Ctrl+N on the bucket-level list opens a modal that creates a bucket with two essential fields
  (Name, Max Age), and the new bucket appears highlighted in the list afterward.
- Reuse the multi-field-dialog pattern `add-kv-bucket-create` established (labeled fields,
  per-field validation, a single `Create` button, Esc-cancels, reopen-seeded-on-failure) rather
  than inventing a new one.
- Keep field parsing (Max Age duration) in a dedicated, individually testable method, matching
  `KVStore/NewBucketOptions.TryParseMaxAge`'s precedent.

**Non-Goals:**
- No Advanced fields (Description, Max Bytes, Storage, Number of Replicas, Placement, Metadata,
  Compression) in this slice — deferred, per the proposal, to a later change. Every bucket
  created via this dialog uses the default File storage backend.
- No bucket Edit or Delete, and no object-level mutation of any kind — `nats-obj`'s object-level
  read-only guarantee stays true, and only bucket-level Create is added (bucket-level Edit/Delete
  remain out of scope, mirroring `add-kv-bucket-create`'s identical scope decision).
- No NATS-CLI-style shorthand duration parsing (`7d`) — `TimeSpan.Parse` only, matching
  `add-kv-bucket-create`'s explicit scope decision for the same kind of field.
- No History or Limit Marker TTL fields — Object Store has no concept of per-object revisions or
  delete/purge markers the way a KV entry does, so there is nothing analogous to expose.

## Decisions

**`NewBucketOptions`: a dialog-owned result type, separate from `NatsObjConfig`.** The dialog
returns `ObjStore/NewBucketOptions.cs`, a plain record shaped around user intent:

```csharp
internal sealed record NewBucketOptions(
    string Name,
    TimeSpan? MaxAge);
```

`MaxAge` stays nullable — `null` means "the user left it unset", not a magic `TimeSpan.Zero` the
reader has to know is special. This is the same seam `KVStore/NewBucketOptions.MaxAge`
established, and it's where a future Advanced page's additional fields (`Description`, `MaxBytes`,
`Storage`, `NumberOfReplicas`, `Placement`, `Metadata`, `Compression`) join as their own nullable
CLR-natural types once exposed — `Storage` included, since (unlike `KVStore/NewBucketOptions`)
this slice's dialog never surfaces it at all.

A separate `ToNatsObjConfig()` conversion (co-located in the same file) is the single place that
translates user intent into `NatsObjConfig`'s wire shape, including the explicit-default
substitution described below. `ObjTab` calls `dialogResult.ToNatsObjConfig()` immediately before
`_obj.CreateObjectStoreAsync(...)` — the dialog itself never constructs or references
`NatsObjConfig`.

**Field → widget → parse mapping (dialog side, in terms of `NewBucketOptions`):**

| Field | Widget | Parse | Validity |
|---|---|---|---|
| Name | `TextField` | — | non-empty after trim |
| Max Age | `TextField` | `TryParseMaxAge(string, out TimeSpan? result)` — same shape as `KVStore/NewBucketOptions.TryParseMaxAge`, reimplemented here rather than shared (see below) | field is valid when `TryParseMaxAge` returns `true` |

Unlike `KVStore/CreateBucketDialog`, this dialog has no dropdown at all — just two plain
`TextField`s and the `Create` button. `Storage` is not offered as a choice; every bucket this
dialog creates uses `NatsObjConfig.Storage`'s own documented default (`File`).

**`TryParseMaxAge`-shaped parsing is reimplemented in `ObjStore`, not shared with `KVStore` or
`Streams`.** All three modules need "empty string means null, otherwise parse a `TimeSpan` or
fail", but there's no existing shared home for dialog-field-parsing helpers, and the call sites
are small enough that extracting a shared helper now would be speculative — same reasoning
`add-kv-bucket-create`'s design.md gave for not sharing with `Streams`. If a fourth copy is ever
needed, that's the trigger to extract a shared `Components`-level helper; not before.

**`MaxAge` unset means unlimited — no substitution needed.** `NatsObjConfig.MaxAge`'s CLR default
of `TimeSpan.Zero` is documented (mirroring `NatsKVConfig.MaxAge`) as "unlimited" — a genuine,
safe "unset" value, not a landmine the way `NatsKVConfig.History`'s `0` default is. `ToNatsObjConfig()`
therefore maps `MaxAge = MaxAge ?? TimeSpan.Zero` with no special-casing.

**Explicit safe defaults for `NatsObjConfig` fields `NewBucketOptions` doesn't carry at all.** A
bare `new NatsObjConfig(name)` leaves `NumberOfReplicas` at the CLR default of `0`.
`ToNatsObjConfig()` unconditionally sets `NumberOfReplicas = 1` (a bucket with zero replicas
isn't a meaningful request), the same reasoning `add-kv-bucket-create` applied to
`NatsKVConfig.NumberOfReplicas`. `Storage` is left unset, which resolves to `NatsObjConfig`'s own
documented default (`File`) — a genuine, safe default rather than a landmine, the same shape as
`MaxAge`'s CLR-default `TimeSpan.Zero` below, so `ToNatsObjConfig()` doesn't need to assign it at
all. `MaxBytes` is left at its CLR default for this slice, matching `add-kv-bucket-create`'s
identical open-question treatment of `NatsKVConfig.MaxBytes` — its zero-vs-unlimited semantics
aren't yet confirmed against the installed 2.8.2 client, flagged as an open question rather than
guessed at. `Description`, `Placement`, `Metadata`, and `Compression` are left unset (their CLR
defaults — `null`/`false`) since none of them carry a "0-means-something-unusable" risk the way
`NumberOfReplicas` does.

**Dialog shape: `Dialog<NewBucketOptions>` with a single `Create` button; Esc cancels.** Same
shape as `KVStore/CreateBucketDialog`: Tab moves between fields, Enter on a field is swallowed via
`OnAccepting` rather than closing the dialog, `Create` is disabled while any field is invalid, no
separate `Cancel` button beyond the mnemonic-less one for mouse users (Esc already cancels via
`Dialog<T>`'s built-in behavior).

**Ctrl+N lives on `ObjStore/BucketListView.cs`, not the shared base.** `DrillableListView<T>`
stays Refresh-only (it's also the base for `ObjStore/ObjectListView`, which must remain
create-less per `nats-obj`'s object-level read-only guarantee). `BucketListView` adds its own
`Command.New` + `Key.N.WithCtrl` binding, the same way `KVStore/BucketListView` layers its own
Ctrl+N on the shared base, and raises a `CreateRequested` event that `ObjTab` handles: run the
dialog, translate via `ToNatsObjConfig()`, call `CreateObjectStoreAsync`, and on success re-run
`RefreshListAsync()` so `ReplaceItems`'s existing identity-preserving logic highlights the new
bucket (`ObjTab.RefreshListAsync` gains an optional `selectName` parameter, mirroring
`KvTab.RefreshListAsync`'s existing shape — `ObjTab`'s version doesn't have one yet since the tab
has never had a create path before).

**Errors surface via a modal `MessageBox.ErrorQuery`, not `StatusChanged`.** Same rationale as
`add-kv-bucket-create`: `StatusChanged` is for passive, transient poll/refresh failures; a failed
`CreateObjectStoreAsync` is the direct result of an explicit user action and deserves a blocking
acknowledgment. `ObjTab` catches the exception, calls
`MessageBox.ErrorQuery(App!, DialogText.Pad("Create Bucket Failed"), DialogText.Pad(ex.Message),
"_Ok")`, and on dismissal reopens `CreateBucketDialog` seeded with the just-entered
`NewBucketOptions` so nothing typed is lost.

## Risks / Trade-offs

- **[Risk]** `NatsObjConfig.MaxBytes`'s zero-vs-unlimited semantics aren't confirmed against the
  installed client version, and this slice leaves it untouched → **Mitigation**: the field is
  outside this slice's exposed surface (Advanced-only), so any landmine there is inherited
  unchanged from server defaults, not introduced by this change — same mitigation
  `add-kv-bucket-create` used for the KV analog of this same open question.
- **[Risk]** `TryParseMaxAge` duplicated a third time (`Streams`, `KVStore`, now `ObjStore`)
  rather than shared → **Mitigation**: accepted per the "no speculative extraction" decision
  above; each copy is a few lines and trivially keeps in sync by inspection if one changes. This
  is the trigger point `add-kv-bucket-create`'s design.md predicted for extracting a shared
  helper — deferred here too, to keep this slice's diff scoped to the OBJ tab only.
- **[Risk]** No server-side validation of a user-entered bucket name beyond client-side non-empty,
  so invalid names (NATS restricts bucket names to a limited character set, same as KV/stream
  names) only fail after the round-trip → **Mitigation**: acceptable for this slice, same
  reasoning `add-kv-bucket-create` used — the failure surfaces via `MessageBox.ErrorQuery` and the
  dialog reopens pre-seeded for a quick fix.

## Migration Plan

No data migration. Purely additive UI/behavior; existing buckets and the object-level read-only
behavior are unaffected. No feature flag — ships as soon as merged.

## Open Questions

- Confirm `NatsObjConfig`'s CLR-default `MaxBytes` (0) is not silently coerced to a usable value
  by the client/server before assuming the untouched CLR default is safe to leave for this slice
  — same open question `add-kv-bucket-create` left for `NatsKVConfig.MaxBytes`, still unresolved
  there; not blocking since `MaxBytes` isn't exposed in either dialog's field set.
