## 1. Data model

- [x] 1.1 Add `object? Group = null` and `int? Priority = null` to `ShortcutHint`
      (`src/lazynats/Components/IShortcutSource.cs`)

## 2. Aggregation defaulting

- [x] 2.1 In `ShortcutAggregator.Collect`, default each yielded hint's `Group` to the
      `IShortcutSource` instance it came from when the hint's own `Group` is null
      (`src/lazynats/Components/ShortcutAggregator.cs`)

## 3. Picker ordering

- [x] 3.1 Replace `ShortcutPickerDialog`'s `OrderBy(hint => hint.Text, ...)` with: group-by
      `Group` (order-preserving), stable-sort each group by `Priority ?? original position`,
      flatten back into one list in group-encounter order
      (`src/lazynats/Components/ShortcutPickerDialog.cs`)
- [x] 3.2 Confirm the dialog's existing 1:1 index alignment between the ordered hint list and
      its rendered rows (`sorted[index]` in the `Accepting` handler) still holds unchanged

## 4. First explicit-group user

- [x] 4.1 In `TemplatesTab`, tag `_extraOperations` (Export, Import) with a distinct `Group` so
      they never interleave with the list's own `TabOperations` (`src/lazynats/Templates/
      TemplatesTab.cs`)

## 5. Verification

- [x] 5.1 Via tmux: open the shortcut picker (`?`) from a view whose ancestors advertise
      multiple sources (e.g. Values tab with `KeyListView` focused) and confirm shortcuts are no
      longer alphabetized, but grouped/ordered per source as designed
- [x] 5.2 Via tmux: open the shortcut picker from the Templates tab and confirm list-CRUD
      shortcuts and Export/Import stay in their own contiguous, non-interleaved blocks
- [x] 5.3 Confirm no visual change to the picker itself (no separators/headers/group names
      rendered) and that Up/Down/Enter/Esc behavior is unchanged
