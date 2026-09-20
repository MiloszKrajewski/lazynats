## 1. `DrillableListView<T>` base

- [x] 1.1 Remove `protected virtual string ItemNoun => "Item";` and `protected virtual string
      FilterDialogTitle => "Filter";`.
- [x] 1.2 Add private fields to hold each activated operation's label (e.g. `_createLabel`,
      `_deleteLabel`, `_editLabel`, `_filterLabel`), all `string?`.
- [x] 1.3 Change `EnableCreate()` to `EnableCreate(string label)`, storing `label` in
      `_createLabel`. Change `EnableDelete()`/`EnableEdit()`/`EnableFilter()` the same way
      (`_deleteLabel`/`_editLabel`/`_filterLabel`). `EnableAscend()` is unchanged (no label — Back
      stays plain).
- [x] 1.4 In `OpenFilterDialog`, use `_filterLabel!` as the `PatternDialog`'s title instead of
      `FilterDialogTitle`.
- [x] 1.5 In `TabOperations`, use `_createLabel!`/`_deleteLabel!`/`_editLabel!`/`_filterLabel!`
      verbatim as each hint's text instead of a composed/derived string.

## 2. `ListEditorView<T>` base

- [x] 2.1 Remove `protected virtual string ItemNoun => "Item";` and `protected virtual string
      FilterDialogTitle => "Filter";`.
- [x] 2.2 Add `string createLabel, string editLabel, string deleteLabel` as required constructor
      parameters (positioned before the existing optional `bindSharedKeys`/`textColor`
      parameters), stored in private fields (e.g. `_createLabel`/`_editLabel`/`_deleteLabel`).
- [x] 2.3 Change `EnableFilter()` to `EnableFilter(string label)`, storing it in a `_filterLabel`
      field.
- [x] 2.4 In `OpenFilterDialog`, use `_filterLabel!` as the `PatternDialog`'s title.
- [x] 2.5 In both `Shortcuts` (standalone/`_bindSharedKeys` branch) and `TabOperations`, use
      `_createLabel`/`_editLabel`/`_deleteLabel`/`_filterLabel` verbatim as each hint's text.

## 3. Per-subclass labels — Streams tab

- [x] 3.1 `StreamListView`: remove its `ItemNoun`/`FilterDialogTitle` overrides; change
      `EnableCreate()`/`EnableDelete()`/`EnableEdit()`/`EnableFilter()` calls to pass "Add new
      Stream" / "Delete Stream" / "Edit Stream" / "Filter Streams" respectively.
- [x] 3.2 `ConsumerListView`: same, with "Add new Consumer" / "Delete Consumer" / "Edit Consumer"
      / "Filter Consumers". `EnableAscend()` call is unchanged.

## 4. Per-subclass labels — Values tab

- [x] 4.1 `Values/BucketListView`: "Add new Bucket" / "Delete Bucket" / "Edit Bucket" / "Filter
      Buckets".
- [x] 4.2 `KeyListView`: "Add new Key" / "Delete Key" / "Edit Key" / "Filter Keys".
      `EnableAscend()` call is unchanged; its own `V -> "View"` hint is untouched.

## 5. Per-subclass labels — Objects tab

- [x] 5.1 `Objects/BucketListView`: "Add new Bucket" / "Delete Bucket" / "Edit Bucket" / "Filter
      Buckets".
- [x] 5.2 `ObjectListView`: "Add new Object" / "Delete Object" / "Filter Objects" (no `EnableEdit`
      call — this list has never activated edit). `EnableAscend()` call and its own `S ->
      "Download"` hint are unchanged.

## 6. Per-subclass labels — Templates, Subscribe, Publish

- [x] 6.1 `TemplateListView`: "Add new Template" / "Delete Template" / "Edit Template" / "Filter
      Templates".
- [x] 6.2 `SubscriptionsView`: update its `base(...)` call to pass "Add new Subscription", "Edit
      Subscription", "Delete Subscription" as the new required `createLabel`/`editLabel`/
      `deleteLabel` constructor arguments (positioned before its existing
      `bindSharedKeys: false, textColor: Theme.SubjectColor` arguments). No `EnableFilter` call
      (unchanged — this list has never activated filtering).
- [x] 6.3 `HeaderEditorView`: same shape, passing "Add new Header", "Edit Header", "Delete Header"
      to its `base(...)` call. No `EnableFilter` call (unchanged).

## 7. Verification

- [x] 7.1 `dotnet build src/lazynats.sln` succeeds with no new warnings.
- [x] 7.2 Via tmux (per `CLAUDE.md`'s UI-testing guidance), open the shortcut picker (`?`) on a
      representative sample — Streams list, Consumers list (drilled in), Subscribe list, and
      Publish's header editor — and confirm the labels read exactly as they did after the prior
      `ItemNoun`-based implementation (same visible text, different mechanism underneath).
- [x] 7.3 Confirm `Back` still reads plain `"Back"` on a drilled-in level (e.g. Consumers).
- [x] 7.4 `openspec validate --changes descriptive-shortcut-names --strict` passes.
