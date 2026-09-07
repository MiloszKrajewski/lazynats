## Why

Every drillable list that supports creating a new item while empty (buckets, keys, objects,
streams, consumers) shows an empty-state hint that only mentions `R to refresh`, silently
omitting that `N` also creates a new item there. `TemplateListView` already gets this right
(`"No templates - N to add one"`), but it omits refresh. The hint text should tell the user
about every action actually available on an empty list, consistently across all six list views.

## What Changes

- Update `EmptyHintText` in `Objects/BucketListView.cs`, `Values/BucketListView.cs`,
  `Values/KeyListView.cs`, `Objects/ObjectListView.cs`, `Streams/StreamListView.cs`, and
  `Streams/ConsumerListView.cs` to mention both `N to add one` and `R to refresh`, each phrased
  for its own item type (bucket/key/object/stream/consumer).
- Update `Templates/TemplateListView.cs`'s `EmptyHintText` to also mention `R to refresh`
  alongside its existing `N to add one`, for the same consistency.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `drillable-list`: the "Empty-State Hint" requirement gains an explicit rule that a subclass's
  hint text must mention every operation actually available on the empty list (at minimum
  refresh, and creation when the subclass has creation enabled), so the wording can't silently
  drift out of sync with what the list supports the way it had for six of the seven subclasses.

## Impact

- Affected files: `src/lazynats/Objects/BucketListView.cs`, `src/lazynats/Values/BucketListView.cs`,
  `src/lazynats/Values/KeyListView.cs`, `src/lazynats/Objects/ObjectListView.cs`,
  `src/lazynats/Streams/StreamListView.cs`, `src/lazynats/Streams/ConsumerListView.cs`,
  `src/lazynats/Templates/TemplateListView.cs`.
- No behavior change beyond the displayed text - `N` and `R` already work on all of these lists
  today (all seven call `EnableCreate()`, and refresh is unconditionally available on every
  `DrillableListView`).
