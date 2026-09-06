## 1. FilterBox: `/`-only activation and generic exit

- [x] 1.1 Add a snapshot field to `FilterBox` (e.g. `_snapshot`), captured whenever the field is
      activated
- [x] 1.2 Change `FilterBox` so it starts `CanFocus = false`, and add an `Activate()`-style entry
      point (sets `CanFocus = true`, captures the snapshot, then `SetFocus()`) for `/` to call
      instead of calling `box.Focus()` directly
- [x] 1.3 Override `FilterBox.OnHasFocusChanged` to set `CanFocus = false` whenever focus leaves the
      field (`newHasFocus == false`), covering Enter/Esc/Tab/arrow/mouse-click-away uniformly
- [x] 1.4 Update the field's Esc handler: non-empty text (or text differing from the snapshot)
      reverts to the snapshot and re-applies it via `IFilterable.ApplyFilter`, then calls
      `FocusList()`; an already-empty field still prefers `HandleEmptySearchEscape()` (ascend) where
      available
- [x] 1.5 Update `DrillableListView<T>.HandleEmptySearchEscape()` / `ListEditorView<T>`'s equivalent
      so the no-ascend-wired case falls back to `FocusList()` instead of doing nothing (fixes the
      pre-existing stuck-focus bug)
- [x] 1.6 Remove the Up-arrow-at-list-top → `box.Focus()` binding from `AttachFilterBox` in both
      `DrillableListView<T>` and `ListEditorView<T>`
- [x] 1.7 In `ManagementTabs.AdvanceWithinPage`, delete the entry branch
      (`IFilterable.AttachedFilterBox → box.Focus()`) and keep the exit branch
      (`FilterBox.Target → target.FocusList()`)

## 2. Bare-letter shortcuts (tab-hosted usage only)

- [x] 2.1 In `DrillableListView<T>`'s shared Create/Delete/Edit/Filter wiring, rebind
      Ctrl+N/Ctrl+D/Ctrl+E/Ctrl+F to bare N/D/E/F
- [x] 2.2 In `DrillableListView<T>`'s `Manual Refresh` wiring (surfaced via `TabOperations`), confirm
      the owning tab dispatches bare R instead of Ctrl+R (the list itself never bound the key — this
      is a `tab-scoped-list-shortcuts` dispatch-side change, see 2.3)
- [x] 2.3 In every tab that dispatches `tab-scoped-list-shortcuts` operations
      (`StreamsTab`, `ValuesTab`, `ObjectsTab`, `TemplatesTab`, `SubscribeTab`), confirm dispatch now
      matches against bare N/D/R/E/F rather than the Ctrl-modified keys (the match is driven by each
      list's advertised `TabOperations`, so this should fall out of 2.1/2.4 without separate tab-side
      code changes — verify, don't assume)
- [x] 2.4 In `ListEditorView<T>`'s tab-hosted path (`bindSharedKeys: false`, e.g. `SubscriptionsView`
      via `SubscribeTab`), rebind its `TabOperations`-advertised keys from Ctrl+N/E/D to bare N/E/D
- [x] 2.5 Confirm `ListEditorView<T>`'s standalone path (`bindSharedKeys: true`, `HeaderEditorView`
      inside `PublishDialog`) is left untouched — still binds Ctrl+N/E/D/F directly
- [x] 2.6 In `ObjectListView`, rebind Ctrl+S (Download) to bare S
- [x] 2.7 In `TemplatesTab`, rebind Ctrl+X (Export) and Ctrl+O (Import) to bare X and O

## 3. Shortcuts-picker trigger: Alt+K → `?`

- [x] 3.1 In `MainWindow.cs`, change the `topLevelShortcuts` entry from `Key.K.WithAlt` to
      `new Key('?')`, and update its rationale comment (the old one explains why Alt+K was chosen
      over Ctrl+/, Alt+/, F1 — replace with why `?` is safe now: depth-first key dispatch always
      gives a focused text field first refusal, see design.md)
- [x] 3.2 Update the status-bar `Shortcut` widget built from that same list to reflect the new key
      (should fall out of 3.1 automatically, since the widget is built from `topLevelShortcuts` —
      verify, don't assume)

## 4. Interactive verification (tmux)

- [x] 4.1 Streams tab: verify R/N/D/E/F all work at the stream level and the consumer level, `/`
      only focuses search via `/` (not Up-arrow, not Tab), Enter/Esc/Tab/arrow/click-away all leave
      search unfocusable, and Esc reverts an in-progress query rather than always clearing it
- [x] 4.2 Values tab: same checks at the bucket level and the key level (bucket-list F vs. key-level
      server-scoped F — both still work under their new bare key)
- [x] 4.3 Objects tab: same checks at the bucket level and the object level, plus bare S (download)
      and bare N (upload, not create-bucket) at the right levels
- [x] 4.4 Templates tab: same checks, plus bare X (export) and O (import) at the tab level
- [x] 4.5 Subscribe tab: verify bare N/E/D work (no `FilterBox` on this tab, so no search-related
      checks apply here)
- [x] 4.6 Confirm typing into any modal dialog's text field (e.g. `CreateStreamDialog`'s Name field,
      `PublishDialog`'s Subject field) is completely unaffected — bare letters typed there must never
      trigger a tab-level shortcut
- [x] 4.7 Confirm the empty-Esc stuck-focus bug is actually fixed: press `/` then Esc immediately at
      a top-level list (e.g. Streams' stream level) and verify focus returns to the list
- [x] 4.8 Confirm `?` opens the Shortcuts picker from every tab and from the live feed, and that
      typing a literal `?` into `FilterBox` (quick-search) or into `PatternDialog`'s pattern field
      (where `?` is a real wildcard character in the filter grammar) types the character instead of
      opening the picker
