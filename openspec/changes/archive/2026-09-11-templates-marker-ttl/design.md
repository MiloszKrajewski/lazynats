## Context

`TemplatesTab.cs` owns all reads/writes/deletes against the `lazynats-templates` KV bucket
directly (see `nats-templates` spec and its own file-header comment — no separate repository
layer). Today:

```csharp
private static readonly NatsKVConfig BucketConfig = new(BucketName) { Storage = NatsKVStorageType.File };
```

`WriteAsync`/`ImportAsync` obtain the store via `_kv.CreateStoreAsync(BucketConfig)`;
`TryDeleteAsync` obtains it via `_kv.GetStoreAsync(BucketName)` and deletes with
`store.DeleteAsync(name)`.

A KV delete writes a tombstone (a `KV-Operation: DEL` marked revision) rather than removing
anything — it's a message in the bucket's underlying stream like any other. With no `MaxAge` and
no `LimitMarkerTTL` configured, nothing ever ages that message out: every template a user creates
and later deletes leaves one permanent message in the stream, for the life of the bucket.
`lazynats-templates` deliberately has no `MaxAge` (a saved template shouldn't expire on its own),
so unlike a bucket that does set `MaxAge` — where a tombstone ages out on the same clock as any
other message — there is no backstop today.

The installed client (`NATS.Client.KeyValueStore` 3.2.0, confirmed against its shipped XML docs)
exposes exactly the mechanism this needs: `NatsKVConfig.LimitMarkerTTL` ("How long the bucket
keeps markers when keys are removed by the TTL setting, 0 meaning markers are not supported" —
NATS server v2.11+) and `PurgeAsync(string key, TimeSpan ttl, NatsKVDeleteOpts opts, ...)`
("Time to live for the purge marker... requires `LimitMarkerTTL` to be set"). Purge additionally
removes prior revisions immediately, rather than waiting on `MaxAge`/`LimitMarkerTTL` to reclaim
them.

## Goals / Non-Goals

**Goals:**
- A deleted template leaves no permanently-retained message in the `lazynats-templates` stream.
- The bucket's config is upgraded to carry `LimitMarkerTTL` regardless of which mutation
  (`Write`/`Import`/`Delete`) a user hits first after upgrading to a build with this change —
  no dependency on write-before-delete ordering.

**Non-Goals:**
- No change to the stored template document shape, to `MaxAge` (still unset — templates don't
  expire on their own), or to `History`.
- No change to the general Values tab's KV delete behavior (`DeleteAsync`, soft delete) —
  `add-kv-key-crud/design.md`'s choice there was deliberate, for general user KV data where
  reversibility-in-principle matters; `lazynats-templates` is app-owned config data with no such
  expectation, so diverging here doesn't reopen that decision.
- No detection of NATS server version; an unsupported server's rejection/silent-ignore of
  `LimitMarkerTTL` surfaces (or doesn't) exactly as it already does for the general
  Create-Bucket-dialog path.

## Decisions

**`BucketConfig` gains a shared `MarkerTtl` constant, used both for the bucket's
`LimitMarkerTTL` and for every `PurgeAsync` call's `ttl` argument.**
One `TimeSpan.FromMinutes(1)` constant rather than two independent literals, so the bucket-level
retention window and the per-purge marker TTL can't drift apart by editing one and forgetting the
other.

**All three mutation call sites move to `CreateOrUpdateStoreAsync(BucketConfig)`.**
`WriteAsync`/`ImportAsync` already call `CreateStoreAsync(BucketConfig)` — whose own doc comment
("Create a new Key Value Store or get an existing one") does not say whether it applies a changed
config to an already-existing bucket. `CreateOrUpdateStoreAsync` unambiguously does
("Creates ... if it doesn't exist or update if the store already exists"), so switching closes the
gap for users who already have a `lazynats-templates` bucket from before this change.
`TryDeleteAsync` moves off `GetStoreAsync(BucketName)` to the same call for the same reason: if a
user's first interaction with the bucket after upgrading is a Delete rather than a Write/Import,
the bucket still needs its config upgraded before the `PurgeAsync(..., MarkerTtl)` call runs
against it, or the ttl argument lands on a bucket that doesn't support marker TTL yet. Routing all
three through the same call keeps the upgrade independent of which operation the user does first.

**Delete Template switches `DeleteAsync(name)` → `PurgeAsync(name, MarkerTtl)`.**
Purge removes prior revisions immediately (rather than only aging out via `MaxAge`, which this
bucket doesn't set) and writes a marker that itself expires after `MarkerTtl` (rather than a
tombstone kept forever). This is the direct fix for the permanent-message problem described above.

**1 minute for `MarkerTtl`.** Long enough that a concurrent watcher/poller reading the bucket
around the moment of deletion still sees the marker (so "was this just deleted" stays
observable briefly), short enough that it doesn't meaningfully delay reclaiming the stream space a
frequently-edited template bucket would otherwise accumulate.

## Risks / Trade-offs

- **[Risk]** `LimitMarkerTTL`/marker-TTL purge requires NATS server v2.11+; against an older
  server the value may be rejected or silently ignored (unconfirmed pre-2.11 behavior — flagged,
  not newly introduced, by `add-kv-bucket-create/design.md`) → **Mitigation**: a rejection surfaces
  through the existing `MessageBox.ErrorQuery` path on Write/Import/Delete failure, same as any
  other server error; silent-ignore (if that's the actual pre-2.11 behavior) leaves today's
  forever-tombstone behavior unchanged rather than making anything worse.
- **[Risk]** `CreateOrUpdateStoreAsync` on every `TryDeleteAsync` call is one extra round-trip
  (config update) compared to today's plain `GetStoreAsync`, on a path that's only ever invoked
  once per explicit user delete → **Mitigation**: accepted; delete is not a hot path, and the
  correctness gain (guaranteed config upgrade regardless of operation order) outweighs one extra
  request on a user-triggered, already-confirmed action.
- **[Risk]** Existing `lazynats-templates` buckets accumulated tombstones before this change ships
  → **Mitigation**: out of scope — this change stops new accumulation going forward; a one-time
  cleanup of already-existing tombstones (if ever needed) is a separate, manual concern (e.g. via
  the `nats` CLI), not something the app does automatically.

## Migration Plan

No data migration in-app. The bucket config upgrade happens lazily, the next time a user performs
any of Write/Import/Delete against an existing `lazynats-templates` bucket — no explicit migration
step, no feature flag. Purely additive/corrective; ships as soon as merged.

## Open Questions

None outstanding — the client-side API surface (`LimitMarkerTTL`, `PurgeAsync(key, ttl, ...)`,
`CreateOrUpdateStoreAsync`) was confirmed against the installed 3.2.0 package's shipped XML docs
during exploration, not assumed.
