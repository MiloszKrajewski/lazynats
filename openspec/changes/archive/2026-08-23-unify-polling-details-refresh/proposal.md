## Why

`KeyDetails` needed a bolted-on `RefreshNow()` escape hatch (with a hand-rolled equality-based
staleness check) because the key list has no cached per-row data to show instantly on highlight
change, unlike `StreamDetails`/`ConsumerDetails`/`BucketDetails`. That leaves two inconsistent
refresh contracts in one shared base class, and a latent gap where an in-flight poll-interval
fetch for a since-abandoned target has no staleness guard at all. Fetching the current target
immediately on every highlight change — not just when there's nothing cached to show — is a
better default for every detail panel: cached list data can already be stale by up to a poll
interval, so refreshing on switch is strictly more correct, not just a KV-specific workaround.

## What Changes

- `PollingDetailsView<TTarget, TInfo>` treats target changes as an `IObservable<TTarget?>`
  (pushed into by `SetPollTarget`/`ClearPollTarget`), debounced by a fixed 100ms
  `static readonly TimeSpan`.
- That debounced target-change stream is merged with the existing poll-interval tick stream (same
  `_active && _hasTarget` gate as today), and both flow through a single
  `Select(...).Switch()` fetch pipeline — replacing the poll pipeline's `SelectAsync`
  (`Select`+`Concat`) and `RefreshNow`'s manual `EqualityComparer` staleness check with one
  `Switch()`-based mechanism that supersedes/ignores a stale in-flight fetch from either trigger.
- **BREAKING** (internal API only): `RefreshNow()` and `FetchAndShowAsync` are removed from
  `PollingDetailsView`. Every subclass now fetches its target immediately on change, unconditionally
  — `KvTab.OnKeyHighlightChanged` no longer calls `RefreshNow()` explicitly, and
  `StreamsTab`/`ConsumersTab`/`KvTab`'s bucket-level handlers pick up the same always-refresh-on-switch
  behavior automatically. Their existing `Show(cached)` call on highlight change is unchanged — still
  an instant, possibly-stale placeholder — now always followed by a debounced refetch.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `polling-details`: the "Immediate Fetch On Demand" requirement changes from a special case
  ("a subclass whose paired list has no cached data") to the universal, debounced default for
  every polling details panel on every target change.

## Impact

- `src/lazynats/Components/PollingDetailsView.cs` — target-change plumbing, poll pipeline, and
  `RefreshNow`/`FetchAndShowAsync` removal.
- `src/lazynats/Kv/KvTab.cs` — drops the explicit `RefreshNow()` call in `OnKeyHighlightChanged`.
- `src/lazynats/Streams/StreamsTab.cs`, `src/lazynats/Streams/StreamDetails.cs`,
  `src/lazynats/Streams/ConsumerDetails.cs` — no code change expected, but behavior changes: highlight
  changes now always trigger an immediate (debounced) refetch instead of waiting for the next poll tick.
- `src/lazynats/Core/AsyncExtensions.cs` — `SelectAsync` becomes unused by `PollingDetailsView` (kept
  as a generic helper for future use per its own comment); no signature changes.
- `openspec/specs/polling-details/spec.md` — delta spec for the "Immediate Fetch On Demand"
  requirement.
