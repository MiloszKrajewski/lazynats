## 1. Bucket configuration

- [x] 1.1 In `src/lazynats/Templates/TemplatesTab.cs`, add a shared `MarkerTtl` constant
      (`TimeSpan.FromMinutes(1)`) next to `BucketName`/`BucketConfig`.
- [x] 1.2 Set `LimitMarkerTTL = MarkerTtl` on `BucketConfig`.

## 2. Upgrade-safe store access

- [x] 2.1 Change `WriteAsync` to obtain the store via `_kv.CreateOrUpdateStoreAsync(BucketConfig)`
      instead of `_kv.CreateStoreAsync(BucketConfig)`.
- [x] 2.2 Change `ImportAsync` to obtain the store via `_kv.CreateOrUpdateStoreAsync(BucketConfig)`
      instead of `_kv.CreateStoreAsync(BucketConfig)`.
- [x] 2.3 Change `TryDeleteAsync` to obtain the store via
      `_kv.CreateOrUpdateStoreAsync(BucketConfig)` instead of `_kv.GetStoreAsync(BucketName)`.

## 3. Purge-based delete

- [x] 3.1 In `TryDeleteAsync`, replace `store.DeleteAsync(name)` with
      `store.PurgeAsync(name, MarkerTtl)`.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 4.2 Manually verify against a live NATS server (v2.11+): with no `lazynats-templates`
      bucket present, create a template via the app, then run
      `nats kv bucket lazynats-templates` (or `nats kv info lazynats-templates`) via the `.bin/`
      `nats` CLI and confirm the bucket's TTL Limit/marker-TTL shows 1 minute.
- [x] 4.3 Manually verify the upgrade path: point the app at (or restore) a `lazynats-templates`
      bucket created by the pre-change binary (marker TTL unset), edit or create a template
      through the app, then re-check via the `nats` CLI that the bucket's marker TTL is now set.
- [x] 4.4 Manually verify delete-only upgrade: with a pre-change bucket (marker TTL unset) and at
      least one existing template, delete a template as the very first interaction (no prior
      write/import in this session) and confirm it succeeds and the marker TTL is applied.
- [x] 4.5 Manually verify a deleted template's underlying message is gone from the stream shortly
      after the marker TTL elapses (e.g. via `nats stream info` / `nats stream view` on the
      bucket's backing stream, `KV_lazynats-templates`), not retained indefinitely as it is today.
