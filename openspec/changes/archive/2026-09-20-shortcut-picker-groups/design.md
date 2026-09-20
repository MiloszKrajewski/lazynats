## Context

`ShortcutPickerDialog` (bound to `?` via `ShortcutPickerLauncher`) collects the currently
available shortcuts via `ShortcutAggregator.Collect`, which walks the focus chain from the
focused leaf up through `SuperView` ancestors, yielding every `IShortcutSource.Shortcuts` it
finds along the way, in that walk order. Today's dialog immediately discards that order:

```csharp
var sorted = hints.OrderBy(hint => hint.Text, StringComparer.OrdinalIgnoreCase).ToList();
```

Several sources already assemble their `Shortcuts`/`TabOperations` in a deliberate order
(`DrillableListView.TabOperations`: Refresh, New, Delete, Edit, Filter; `TemplatesTab.Shortcuts`:
its list's `TabOperations` concatenated with `_extraOperations` = Export, Import). Alphabetizing
scrambles both. This change replaces that sort with an ordering that respects per-source
clustering and an optional manual override, without introducing any visible grouping UI (no
separators, headers, or group names shown) - see `openspec/changes/shortcut-picker-groups/
proposal.md` for the motivating detail and scope.

## Goals / Non-Goals

**Goals:**
- Preserve each source's deliberate internal ordering of the shortcuts it advertises.
- Let one source split its own advertised shortcuts into more than one ordering cluster (e.g.
  `TemplatesTab` keeping its list's CRUD operations and its Export/Import pair from ever
  interleaving with each other, regardless of future additions to either).
- Provide a small, optional per-hint escape hatch (`Priority`) for the rare case where declared
  order still isn't what should be shown, without requiring every hint to opt in.
- Keep the change additive: sources that don't care about ordering need zero changes.

**Non-Goals:**
- No visual grouping (no dividers, headers, or group names in the picker). This is an
  ordering-only change; introducing visible groups is a separate, later change if wanted.
- No change to `ListView` navigation, wraparound, or the `Accepting`/`RequestStop` flow -
  the picker stays a single flat list, index-aligned 1:1 with its backing `ShortcutHint` list,
  exactly as today.
- No change to which shortcuts are collected (`ShortcutAggregator.Collect`'s focus-chain walk
  itself is untouched) - only how the collected set is ordered before rendering.

## Decisions

### `Group` is an opaque identity (`object?`), not a display string
`ShortcutHint` gains `object? Group = null`. Since no group name is ever shown (a deliberate
non-goal), there's no reason to force callers to invent a string - reference identity is exactly
what "keep these hints clustered together" needs.
- Alternative considered: `string? Group`. Rejected for now - it would require every default
  assignment to synthesize a name (e.g. from a type) purely for future display, adding code with
  no present payoff. Because `Group` is `object?`, switching to a display-friendly string later
  (e.g. once a visible-groups follow-up change is proposed) is a localized, additive change to
  the same field - not a redesign.

### Default `Group` is assigned by `ShortcutAggregator.Collect`, not at each call site
`Collect` already iterates per-source (`for (var view = focused; ...) if (view is
IShortcutSource source) foreach (var hint in source.Shortcuts) ...`). It's extended to stamp
`hint.Group ??= source` for every hint whose own `Group` is still null, before yielding it. This
means the large majority of existing `IShortcutSource` implementers (`LiveUpdatesView`,
`ObjectListView`, `KeyListView`, `PayloadDetailSection`, every tab forwarding
`TabOperations`) need no changes at all - they get "one group per source" for free, purely from
their existing position in the focus-chain walk.
- Alternative considered: require each call site to set `Group` explicitly. Rejected - it would
  touch every existing `new ShortcutHint(...)` for no behavioral benefit, working against the
  "additive, opt-in" goal.

### Ordering: order-preserving group-by, then per-group stable sort by `Priority ?? position`
`ShortcutPickerDialog` replaces its `OrderBy(hint => hint.Text)` with:
1. `hints.GroupBy(hint => hint.Group)` - LINQ's `GroupBy` preserves first-occurrence order of
   both groups and of elements within a group, so this alone reproduces today's walk order
   without additional bookkeeping.
2. Within each group, a stable sort by `Priority ?? int.MaxValue` (using each hint's index in
   step 1's group as the tiebreaker, since `OrderBy` is stable) - hints without a `Priority`
   keep their relative declared order; a hint with an explicit `Priority` is pulled earlier
   within its own group only.
3. Flatten groups back into one list, groups still in first-occurrence order.

`Priority` deliberately cannot move a hint into a different group's span - it only reorders
within the group boundary established in step 1. This keeps a "group" a contiguous, predictable
unit even though nothing marks its boundary visually, which matters for anyone reasoning about
where a hint will land, and keeps the door open for a future visible-groups change to draw
dividers at exactly these same boundaries without re-deriving the ordering logic.
- Alternative considered: a single global `OrderBy(Priority ?? position)` with no group
  scoping. Rejected - it would let one source's `Priority` drag a hint into the middle of
  another source's cluster, which is exactly the interleaving this change exists to prevent.

### `TemplatesTab` is the first (and for now, only) explicit `Group` user
`TemplatesTab.Shortcuts => _listView.TabOperations.Concat(_extraOperations)` tags
`_extraOperations` (Export, Import) with a distinct `Group` (e.g. a small sentinel object owned
by `TemplatesTab`) so they never interleave with the list's own CRUD hints, matching their
current declared order. Every other existing source is left as-is, relying on the aggregator's
per-source default.

## Risks / Trade-offs

- [`GroupBy`'s ordering guarantee is behavioral, not a written contract in the same sense as an
  explicit sort] -> Mitigation: this is documented, longstanding LINQ-to-Objects behavior (not
  Parallel LINQ, which is explicitly unordered); a comment at the call site notes the
  dependency so a future reader doesn't "simplify" it into a plain `Distinct`/`Dictionary` pass
  that drops ordering.
- [A hint whose `Group` default (the source instance) happens to not implement useful equality]
  -> Mitigation: default reference equality on the `IShortcutSource` instance is exactly what's
  wanted (same instance => same group); no custom equality needed since `Group` is never
  serialized or compared across dialog opens.
- [Future visible-groups work might want `Group` to carry a name after all] -> Mitigation: noted
  under the `Group` type decision above - changing `object?` to something string-backed later is
  additive to this same field, not a rework of the ordering logic in `ShortcutPickerDialog`.

## Open Questions
(none - the ordering-only scope keeps this fully resolved)
