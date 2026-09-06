## Why

`doc/UI.md` reserves a `4:KV` management tab for browsing JetStream Key/Value stores, and it's the
next unbuilt tab now that Streams (`3:Streams`) has landed. Without it, inspecting KV bucket state
and key contents requires leaving the app for the `nats` CLI.

## What Changes

- Add a `4:KV` management tab, read-only, following the Streams tab's LHS list / RHS details,
  two-level drill-down shape (bucket list → Enter → key list, Esc/Backspace back up).
- Bucket level: list bucket names (`NatsKVContext.GetStatusesAsync`), load-once + manual Ctrl+R
  refresh (no auto-refresh), RHS shows bucket stats (message/byte counts, compression, TTL)
  refreshed periodically while a bucket is highlighted.
- Key level: descending into a bucket fetches its key names fresh every time
  (`NatsKVStore.GetKeysAsync`), same load-once + manual Ctrl+R refresh as the bucket level. RHS
  shows the highlighted key's metadata (revision, created, operation) plus its value, refreshed
  periodically.
- Key value rendering: raw UTF-8 decode of the byte payload, no JSON pretty-printing (matches the
  live feed's current payload rendering — smarter formatting is a separate concern).
- A key that no longer exists on the server by the time a poll tick fires (e.g. deleted by another
  client) is treated the same as nothing being highlighted: the details panel goes blank. No
  distinct "deleted" indicator.
- Extend `PollingDetailsView<TTarget,TInfo>` with an optional large body region rendered below the
  existing label:value header rows, filling the remaining pane height, clipped (not scrollable) to
  whatever fits. Default is no body, so `StreamDetails`/`ConsumerDetails` are unaffected.
- No mutation affordances anywhere in the KV tab (no create/edit/delete for buckets or keys),
  matching the Streams tab's read-only precedent.

## Capabilities

### New Capabilities
- `nats-kv`: Read-only KV management tab — bucket list with stats, drill-down to a bucket's keys
  with metadata + value detail, both levels load-once with manual Ctrl+R refresh and periodic
  detail-panel polling while the tab holds focus.

### Modified Capabilities
- `polling-details`: Adds an optional large body region (below the existing label:value rows,
  filling remaining height) that a subclass may render, alongside the existing rows-only
  rendering used by `StreamDetails`/`ConsumerDetails`.

## Impact

- New `src/lazynats/Kv/` directory: `KvTab`, `BucketListView`, `BucketDetails`, `KeyListView`,
  `KeyDetails`, presenters/identity accessors for bucket and key items — mirroring
  `src/lazynats/Streams/`.
- `src/lazynats/Components/PollingDetailsView.cs`: add the optional body-rendering hook.
- `src/lazynats/MainWindow.cs`: register the new tab (`4:KV`, Alt+4) alongside the existing three.
- `src/lazynats/Services.cs` / `Program.cs`: resolve `INatsKVContext` for the new tab (KV context
  creation, likely via `NatsConnection.CreateKeyValueContextAsync()`).
- New package usage: `NATS.Client.KeyValueStore` (already referenced in `lazynats.csproj`, not yet
  used anywhere in the codebase).
- `doc/UI.md` stays accurate as written; no doc changes needed.
