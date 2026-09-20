## Why

The shortcut picker (`ShortcutPickerDialog`) sorts every collected shortcut alphabetically by
its display text. That destroys ordering several sources already curate deliberately - e.g.
`DrillableListView.TabOperations`'s Refresh -> New -> Delete -> Edit -> Filter reads as
Delete -> Edit -> Filter -> New -> Refresh once alphabetized, and `TemplatesTab`'s deliberate
list-CRUD-then-Export/Import split gets interleaved the same way. As more shortcuts accumulate
across ancestors in the focus chain, the flattened, alphabetized list gets harder to scan for
the shortcut you're actually after.

## What Changes

- `ShortcutHint` gains two optional fields: `Group` (an opaque identity, not a display string -
  no name is shown anywhere yet) and `Priority` (`int?`).
- `ShortcutAggregator.Collect` defaults a hint's `Group` to the `IShortcutSource` instance that
  yielded it, for any hint that didn't set one itself. Existing call sites are unaffected unless
  they want to split their own advertised shortcuts into more than one cluster (e.g.
  `TemplatesTab` tagging `_extraOperations` separately from its list's `TabOperations`).
- `ShortcutPickerDialog` replaces its alphabetical-by-text sort with: group hints by `Group`
  (preserving first-encounter order across groups), stable-sort each group's hints by
  `Priority ?? original position`, then flatten groups back into one list in that encounter
  order.
- No visual change: groups are not displayed, and no separators/dividers are introduced between
  them - this change is ordering-only. The picker remains a single flat `ListView`, and
  navigation (Up/Down, wraparound, Enter-to-select) is unchanged.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `keyboard-shortcut-discovery`: adds requirements for how the currently-available shortcut set
  is ordered before being presented, in terms of grouping and per-hint priority, replacing the
  implicit "alphabetical by text" behavior.

## Impact

- `src/lazynats/Components/IShortcutSource.cs` (`ShortcutHint` record)
- `src/lazynats/Components/ShortcutAggregator.cs` (default `Group` assignment)
- `src/lazynats/Components/ShortcutPickerDialog.cs` (sort/order logic)
- `src/lazynats/Templates/TemplatesTab.cs` (opts `_extraOperations` into its own `Group`, as the
  first real user of explicit grouping)
- No changes to rendering (`ColoredRow`/`ColoredRowRenderer`/`ColoredRowListDataSource`) or to
  `ListView` navigation/key handling.
