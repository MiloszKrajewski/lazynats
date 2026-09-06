## Context

`src/lazynats/Values/BucketName.cs` and `src/lazynats/Objects/BucketName.cs` each independently
encode "a bucket is any stream named `KV_<name>`/`OBJ_<name>`" — the same convention `nats.go`'s
KV/Object wrappers and the `nats` CLI use to name the streams they create. That convention was
deliberately kept as two separate, tab-scoped copies (see the KV tab's archived design doc,
"single place this convention is applied" — scoped per tab, back when there were only two
consumers).

Two things push past that today:

1. **False positives.** The check is currently name-prefix-only. A plain stream a user happens to
   name `KV_orders` (or `OBJ_orders`) for their own subjects is misclassified as a bucket — already
   true today in the Values/Objects tabs (confirmed: `INatsKVContext.GetBucketNamesAsync()` in the
   installed SDK, v2.8.2, uses the exact same name-prefix-only check internally, so this isn't a
   corner the SDK has already covered for us). `INatsObjContext` has no equivalent bulk
   bucket-listing call at all.
2. **A third consumer.** The Streams tab (`src/lazynats/Streams/StreamsTab.cs`) needs to exclude
   KV/Object bucket-backing streams from its list, so the "one copy per tab" scoping no longer
   fits — three copies of the same logic is where duplication stops being fine.

Separately, `DrillableListView<T>.GetIdentity`/`IValuePresenter<T>.Format` are called far more
often than "once per item" — from sort (`OrderBy(GetIdentity, ...)`), every filter keystroke
(`Where(item => filterRegex.IsMatch(GetIdentity(item)))` over the whole list),
`NeighborIdentity`'s binary search, and every row redraw via `PresenterListDataSource.Format`.
Today `Values`/`Objects` re-derive the bare bucket name from the full stream name on every one of
those calls, because their list items are the raw `NatsKVStatus`/`StreamInfo` SDK types, which
carry no pre-stripped name field of their own (`INatsKVStore.Bucket` is clean, but only exists on
the per-bucket store object returned by `GetStoreAsync(bareName)` — which needs the bare name
already, so it can't drive the bulk list).

## Goals / Non-Goals

**Goals:**
- Streams tab (`2:Streams`) excludes any stream that's actually backing a KV or Object Store
  bucket, unconditionally — those already have dedicated tabs (`3:Values`, `4:Objects`).
- Bucket classification requires both the name prefix and a matching reserved subject, closing the
  false-positive gap, for all three tabs' use of it.
- One shared classification helper, replacing the two per-tab copies.
- Values/Objects list items carry their derived bucket name once, computed at refresh time, not
  re-derived on every sort/filter/render.

**Non-Goals:**
- No UI toggle to show KV/OBJ-backing streams in the Streams tab anyway — exclusion is
  unconditional, matching `nats stream ls`'s own default behavior. A toggle can be added later if
  it proves useful.
- Not adversarial-proof: a stream deliberately crafted with both the right name prefix and the
  right subject would still be misclassified. No such signal exists in JetStream's protocol (KV/
  Object Store are entirely client-side conventions on top of plain streams) — matching what the
  reference SDK itself can tell apart is the actual bar here, not cryptographic certainty.
- No changes to consumer-level behavior, or to any create/edit/delete flow on any tab.
- No fix to the SDK's own `GetStatusesAsync()` returning every stream unfiltered (Values) or
  `INatsObjContext` having no bulk bucket-listing call (Objects) — both already worked around
  client-side before this change; this change only tightens the classification used in that
  workaround.

## Decisions

### One shared `BucketName`, in `Components/`
Moves from two tab-owned files (`Values/BucketName.cs`, `Objects/BucketName.cs`) to
`src/lazynats/Components/BucketName.cs`, alongside the other cross-tab shared pieces
(`DrillableListView`, `PollingDetailsView`). All three tabs (`Streams`, `Values`, `Objects`)
consume it directly.

**Alternative considered**: keep three independent copies (one now added for Streams). Rejected —
the original "one place per tab" scoping was explicitly sized for two consumers; a third makes
sharing the better trade-off, and the classification logic itself (regex + subjects check) is
identical in shape for KV and OBJ, differing only in the prefix/subject constants.

### Classification: `TryGetKvBucketName(StreamConfig)` / `TryGetObjBucketName(StreamConfig)` → `string?`
```csharp
internal static partial class BucketName
{
    [GeneratedRegex(@"^KV_(?<bucket>.+)$")]
    private static partial Regex KvStreamName();

    [GeneratedRegex(@"^OBJ_(?<bucket>.+)$")]
    private static partial Regex ObjStreamName();

    public static string? TryGetKvBucketName(StreamConfig config)
    {
        if (config.Name is not { } name) return null;

        var match = KvStreamName().Match(name);
        if (!match.Success) return null;

        var bucket = match.Groups["bucket"].Value;
        var expectedSubject = $"$KV.{bucket}.";
        if (config.Subjects?.Any(s => s.StartsWith(expectedSubject, StringComparison.Ordinal)) != true)
            return null;

        return bucket;
    }

    public static string? TryGetObjBucketName(StreamConfig config) { /* same shape, OBJ_ / $O. */ }
}
```
Takes `StreamConfig` (not `StreamInfo`, not a bespoke type) — `Name` and `Subjects` are all it
needs, and every call site already has a `StreamConfig` in hand (`StreamInfo.Config` /
`NatsKVStatus.Info.Config`). Returns `string?` rather than the classic `bool TryXxx(out T)` shape:
`Try`-prefixed naming still applies (the call can fail), but the boilerplate of an `out` parameter
buys nothing once the success value is itself a reference type where `null` is already an
unambiguous "not a bucket."

The regex is source-generated (`[GeneratedRegex]`) for readability (a named capture group reads
more clearly than manual prefix-length slicing) at the cost of being slower than a plain
`StartsWith`/slice — accepted, since this runs once per stream per refresh, not on a hot path (see
next decision).

**Alternatives considered**:
- Separate `IsKvBucket`/`From` methods (an earlier iteration of this design) — rejected: two
  separate regex matches per stream for what's really one classify-and-extract operation.
- Subjects-only check (no name prefix) — rejected: the name prefix is a cheap, definitive negative
  for the overwhelming majority of streams (this app's non-bucket streams), so checking it first
  avoids reformatting/scanning `Subjects` for streams that obviously aren't buckets.

### `KvBucketItem`/`ObjBucketItem` wrappers carry the derived name
`Values`/`Objects`' list items become small wrapper records —
`KvBucketItem(string Name, NatsKVStatus Status)` and `ObjBucketItem(string Name, StreamInfo Info)`
— built once per item while iterating the refresh's server response:

```csharp
await foreach (var status in _kv.GetStatusesAsync())
    if (BucketName.TryGetKvBucketName(status.Info.Config) is { } name)
        items.Add(new KvBucketItem(name, status));
```

`BucketListView.GetIdentity`/`BucketNamePresenter.Format` then read `.Name` directly — plain field
access instead of re-running the regex+subjects check on every sort, filter keystroke, and row
redraw. `BucketDetails.SetTarget`/`ValuesTab`/`ObjectsTab`'s other call sites that need the bare
name switch from `BucketName.From(...)` to the wrapper's `.Name` the same way.

**Alternative considered**: leave `Values`/`Objects` list items as raw `NatsKVStatus`/`StreamInfo`,
matching how `Streams`/`Consumers` push raw SDK DTOs straight through `DrillableListView<T>`
everywhere else in the app. Rejected per explicit direction — re-deriving a value this frequently,
especially now that derivation is a regex match rather than a string slice, is worth fixing rather
than carrying forward.

### Streams tab: unconditional exclude via the same `TryGet...BucketName` calls
`StreamsTab.RefreshListAsync` filters the fetched stream list, discarding the extracted name
(only the null/non-null distinction matters here):

```csharp
await foreach (var stream in _jetStream.ListStreamsAsync())
    if (BucketName.TryGetKvBucketName(stream.Info.Config) is null
        && BucketName.TryGetObjBucketName(stream.Info.Config) is null)
        streams.Add(stream.Info);
```

No wrapper needed here — the Streams tab already carries `StreamInfo` straight through and has no
"bucket name" concept of its own to cache.

## Risks / Trade-offs

- **[Risk]** Classification is still convention-based, not protocol-guaranteed — a deliberately
  crafted decoy stream (right name prefix, right subject) would still be misclassified. →
  **Mitigation**: accepted; this matches the actual ceiling of what any NATS client (including the
  reference SDK and `nats` CLI) can tell apart, since KV/Object Store have no server-side "kind"
  marker to check instead.
- **[Trade-off]** The Streams tab exclusion is unconditional — there's no way, from within
  lazynats, to inspect a KV/OBJ-backing stream's raw JetStream config (replica count, storage
  backend, etc.) directly. → **Mitigation**: matches `nats stream ls`'s own default behavior;
  revisit with a toggle later if this proves to matter in practice.
- **[Trade-off]** Reverses the KV-tab design doc's explicit "single place this convention is
  applied, scoped per tab" principle. → **Mitigation**: that scoping was sized for two consumers;
  a third consumer (Streams) tips the balance toward sharing, and the shared version is strictly
  more correct (subjects-verified) than either of the two copies it replaces.

## Migration Plan

N/A — purely client-side classification/rendering/filtering logic, no persisted state or
server-side changes. Existing KV/OBJ buckets and streams are unaffected; only what lazynats
displays and how it filters changes.

## Open Questions

None outstanding — scope and shape were confirmed during exploration before this change was
drafted.
