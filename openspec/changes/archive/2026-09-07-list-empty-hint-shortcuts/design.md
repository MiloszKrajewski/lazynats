## Context

This is a small, self-contained text fix, not a cross-cutting or architectural change - no new
dependency, no data model, no security/perf/migration concerns. The only real decision is the
exact wording for each list's hint, since each item type reads slightly differently ("bucket" vs
"key" vs "object" vs "stream" vs "consumer").

## Goals / Non-Goals

**Goals:**
- Pin down the literal hint string for each of the seven `EmptyHintText` overrides.

**Non-Goals:**
- No change to `DrillableListView`'s "Empty-State Hint" requirement or rendering - only the
  subclass-supplied strings change.
- No change to which keys are bound or what they do - `N` and `R` already work today.

## Decisions

Use the existing `TemplateListView` phrasing as the pattern (`"No <items> - N to add one"`) and
extend it with the refresh clause, comma-separated, in `N`-then-`R` order (create is the more
useful action on an empty list, so it leads):

| File | New `EmptyHintText` |
|---|---|
| `Objects/BucketListView.cs` | `"No buckets - N to add one, R to refresh"` |
| `Values/BucketListView.cs` | `"No buckets - N to add one, R to refresh"` |
| `Values/KeyListView.cs` | `"No keys - N to add one, R to refresh"` |
| `Objects/ObjectListView.cs` | `"No objects - N to add one, R to refresh"` |
| `Streams/StreamListView.cs` | `"No streams - N to add one, R to refresh"` |
| `Streams/ConsumerListView.cs` | `"No consumers - N to add one, R to refresh"` |
| `Templates/TemplateListView.cs` | `"No templates - N to add one, R to refresh"` |

## Risks / Trade-offs

[Hardcoded hint text can drift out of sync with actual capabilities again in the future] →
Mitigation: none needed beyond code review - this is display text tied 1:1 to each view's
`EnableCreate()`/base-refresh calls, not derived dynamically, matching the existing pattern this
change is fixing.
