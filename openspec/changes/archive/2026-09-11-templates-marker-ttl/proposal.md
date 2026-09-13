## Why

`lazynats-templates` is created with no `MaxAge` and no `LimitMarkerTTL`, and Delete Template uses
`DeleteAsync` (a soft delete/tombstone write, not a removal). A tombstone is just another message
in the bucket's underlying stream; with no `MaxAge` to age it out and no `LimitMarkerTTL` to expire
it independently, nothing ever removes it — every template a user creates and later deletes leaves
one permanent, unreclaimable message in the stream for the lifetime of the bucket. (A bucket that
has `MaxAge` set doesn't have this problem: a tombstone ages out on the same clock as any other
message. `lazynats-templates` deliberately has no `MaxAge` — a saved template isn't meant to expire
on its own — so it has no such backstop today.)

## What Changes

- `lazynats-templates`' bucket config gains `LimitMarkerTTL` (1 minute), enabling independent
  expiry of delete/purge markers.
- Delete Template switches from `DeleteAsync` (tombstone, kept forever) to `PurgeAsync(key, ttl)`
  (removes prior revisions immediately and writes a purge marker that itself expires after the
  configured TTL) — so a deleted template leaves no lasting trace in the stream.
- The three places that obtain a handle to the bucket for a mutation (`WriteAsync`, `ImportAsync`,
  and now `TryDeleteAsync`) switch from `CreateStoreAsync`/`GetStoreAsync` to
  `CreateOrUpdateStoreAsync(BucketConfig)`, so a bucket created by an older build of the app (with
  no `LimitMarkerTTL`) gets upgraded to the new config the next time any of the three runs — the
  upgrade isn't order-dependent on which operation the user happens to hit first.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `nats-templates`:
  - **Template Storage**: the bucket is now created (and kept up to date on every write/import/
    delete) with `LimitMarkerTTL` set, not just `Storage`.
  - **Delete Template**: deletion is now a purge (immediate removal of prior revisions, marker
    expires after the TTL) rather than a tombstone that's kept forever.

## Impact

- `src/lazynats/Templates/TemplatesTab.cs`: `BucketConfig` gains `LimitMarkerTTL`; a shared
  `MarkerTtl` constant is added; `WriteAsync`/`ImportAsync` move to `CreateOrUpdateStoreAsync`;
  `TryDeleteAsync` moves to `CreateOrUpdateStoreAsync` + `PurgeAsync(name, MarkerTtl)`.
- No API/wire format change to the stored template documents themselves — only bucket config and
  the delete mechanism change.
- Requires NATS server v2.11+ for `LimitMarkerTTL`/marker-TTL purge to take effect (same
  requirement `add-kv-bucket-create` already documented for this client feature); on an older
  server the config value is either rejected or silently ignored, unconfirmed pre-2.11 — pre-existing,
  unconfirmed risk, not introduced by this change.
