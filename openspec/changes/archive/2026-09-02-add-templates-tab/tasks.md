## 1. Data Model & Persistence

- [x] 1.1 Add `Templates/PayloadType.cs`: `enum PayloadType { Json, Text, Base64 }`.
- [x] 1.2 Add `Templates/Template.cs`: an item type carrying Name (the KV key), Subject, Headers
      (`IReadOnlyDictionary<string,string>` or similar), PayloadType, and Payload (`string`) - the
      full value `ListEditorView<Template>` holds per row (list/edit/delete all act on this value
      directly, no separate per-item detail fetch).
- [x] 1.3 Add `Templates/TemplateDocument.cs` (or similar): the JSON-document shape actually
      persisted to KV (Subject/Headers/PayloadType/Payload - no Name, per design.md's "Storage
      shape" decision), plus a source-generated `JsonSerializerContext`
      (`[JsonSerializable(typeof(TemplateDocument))]`) for AOT-safe serialize/deserialize.
      `PayloadType` serializes via `JsonStringEnumConverter` (enum names, not ints).
- [x] 1.4 Add a `TemplatesBucketName` constant (`"lazynats-templates"`) and a fixed
      `NatsKVConfig` (File storage, library defaults otherwise) used by every write path.
- [x] 1.5 Implement a list-fetch helper: `GetStoreAsync(TemplatesBucketName)` (never
      `CreateStoreAsync`), enumerate keys, fetch each entry, deserialize into `Template` values
      (Name = key + deserialized `TemplateDocument` fields). Wrap the whole fetch in one
      try/catch that yields an empty result on any failure (see design.md's "Missing bucket on
      read renders as empty" decision) - do not special-case "bucket not found" vs. other
      failures.
- [x] 1.6 Implement a write helper: `CreateStoreAsync(TemplatesBucketConfig)` (get-or-create, per
      design.md) then `PutAsync(name, serializedDocument)`.
- [x] 1.7 Implement a delete helper: `GetStoreAsync` + `DeleteAsync(name)` (delete only ever
      targets an existing template, so no bucket-creation path needed here).

## 2. Payload Validation

- [x] 2.1 Implement JSON validation (parse via `System.Text.Json.JsonDocument.Parse` or
      equivalent, catching `JsonException`) used when PayloadType is `Json`.
- [x] 2.2 Implement Base64 validation (`Convert.TryFromBase64String` or equivalent) used when
      PayloadType is `Base64`.
- [x] 2.3 `Text` PayloadType: no validation (any string, including empty, is valid).

## 3. Template Dialog (Create/Edit)

- [x] 3.1 Add `Templates/TemplateDialog.cs`, modeled on `PublishDialog`'s field layout (Subject
      `EditFrame`-wrapped `TextField`, `HeaderEditorView` for Headers, `EditFrame`-wrapped
      `TextView` for Payload) plus: a Name field above Subject (a `TextField`, disabled/locked
      when `isEdit`, same convention as `CreateKeyDialog`'s Name), and a Payload Type selector
      between Headers and Payload (defaulting to `Text` on create). Its Headers field, like
      `PublishDialog`'s own, gets no attached `FilterBox` - `HeaderEditorView` never offers
      filter/search, in either dialog - see design.md's "Header editor (revised)" decision -
      giving it a fixed, at-least-3-line frame instead.
- [x] 3.2 Wire Save/Create validity: disabled while Name is empty (create mode only - edit mode's
      Name is fixed and always valid), Subject is empty, or Payload fails the currently-selected
      Payload Type's validation (2.1/2.2). Re-run Payload validation whenever the Payload Type
      selection changes, not only when Payload's text changes.
- [x] 3.3 Visually flag invalid Name/Subject/Payload the same way `PublishDialog`/`CreateKeyDialog`
      do (red foreground via `Theme.EditableBackground`-paired `Attribute`).
- [x] 3.4 Title/button text: " New Template "/"Create" vs. " Edit Template "/"Save", matching
      `CreateKeyDialog`'s isEdit-driven title convention.
- [x] 3.5 Seed the dialog from an existing `Template` in edit mode (Name, Subject, Headers,
      PayloadType, Payload all pre-filled); seed empty in create mode.

## 4. List + Details Tab

(Revised from the original tab-hosted-`ListEditorView<Template>` plan to the same
`DrillableListView<T>` + `PollingDetailsView<TTarget, TInfo>` LHS-list/RHS-details split every
other management tab uses - see design.md's "List hosting (revised)" and "Details pane" decisions.)

- [x] 4.1 Add `Templates/TemplateNamePresenter.cs`: `IValuePresenter<Template>` formatting a row
      as the template's Name.
- [x] 4.2 Add `Templates/TemplateListView.cs`: `DrillableListView<Template>` enabling Create/
      Delete/Edit/Filter (no Descend/Ascend - Templates is flat), `GetIdentity` returning Name.
- [x] 4.3 Add `Templates/TemplateDetails.cs`: `PollingDetailsView<Template, Template>` with
      `PollInterval` overridden to `null` (nothing to poll - the highlighted list item already
      carries the full template) and `BuildRows`/`BuildBody` rendering Name/Subject/Payload Type,
      then Headers + Payload combined as one body string (see design.md).
- [x] 4.4 Add `Templates/TemplatesTab.cs` laying out `TemplateListView` (40%-width, with an
      attached `FilterBox`) and `TemplateDetails` side by side, mirroring
      `StreamsTab`/`ValuesTab`/`ObjectsTab`'s stream/bucket-level layout. Owns the actual
      `lazynats-templates` KV reads/writes/deletes itself, reacting to `CreateRequested`/
      `EditRequested`/`DeleteRequested`/`RefreshRequested` (opening `TemplateDialog` for
      Create/Edit) and `HighlightChanged` (updating the details pane) - the list view itself owns
      only list/search/selection mechanics, per `StreamListView`'s own precedent.
- [x] 4.5 Implement Ctrl+D's confirm-before-delete prompt (`MessageBox.Query`, Cancel as default),
      matching `ValuesTab`'s bucket/key delete prompts.
- [x] 4.6 Fetch the template list (1.5) when the tab first receives keyboard focus, and expose a
      Ctrl+R-triggered re-fetch, preserving highlight-if-still-present / else-first-item, matching
      `nats-kv`'s Manual Bucket List Refresh behavior.
- [x] 4.7 Implement `IShortcutSource`/`ITabOperationsSource` dispatch on `TemplatesTab` per
      `tab-scoped-list-shortcuts`, forwarding Ctrl+N/D/E/R/F to `TemplateListView`'s advertised
      `TabOperations`.
- [x] 4.8 Report create/edit/delete/refresh failures via the same `StatusChanged` event shape
      `StreamsTab`/`ValuesTab`/`ObjectsTab` already expose, for `MainWindow`'s status-bar wiring.
- [x] 4.9 Remove `HeaderEditorView`'s `EnableFilter()` call and `FilterDialogTitle` override, and
      `PublishDialog`'s attached `FilterBox` for it, folding the reclaimed rows into the header
      `EditFrame`'s own height in both dialogs (see design.md's "Header editor (revised)"
      decision). Also remove `ListEditorView<T>`'s now-unused `SelectedItem`/`ReplaceItems`
      members - both existed solely to support the original `ListEditorView<Template>`-hosted
      Templates shape.
- [x] 4.10 Fix `DrillableListView<T>`'s pre-existing "Enter hijacks focus to the search box on a
      leaf list" defect (see design.md's "Bug fix" note and `drillable-list`'s modified "Shared
      Descend Wiring" requirement): remove `EnableDescend()` and its 3 call sites
      (`StreamListView`, `Values.BucketListView`, `Objects.BucketListView`), replacing it with an
      unconditional `AddCommand(Command.Accept, () => { DescendRequested?.Invoke(); return true; })`
      in the base constructor. Verified via `tmux` that descend still works on all three two-level
      tabs and Enter is now a no-op (no focus change) on every leaf list (Templates, Values keys,
      Objects files).

## 5. MainWindow Integration

- [x] 5.1 Construct `TemplatesTab` in `MainWindow.cs` (reusing the already-resolved
      `INatsKVContext`), title `" 5:Templates "`, and add it to `ManagementTabs` after Objects.
- [x] 5.2 Add an Alt+5 entry to `topLevelShortcuts` selecting the Templates tab, matching the
      Alt+1..4 entries.
- [x] 5.3 Add a `templatesStatusShortcut` to the `StatusBar`, wired to `TemplatesTab.StatusChanged`,
      matching `streamsStatusShortcut`/`valuesStatusShortcut`/`objectsStatusShortcut`.

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 6.2 Drive the app via `tmux` (per CLAUDE.md) against a real `nats-server`: confirm
      `lazynats-templates` does not exist yet, Alt+5 shows the Templates tab (list+details split)
      with the empty hint, Ctrl+N creates a template with a header and a payload (bucket now
      exists, confirmed independently e.g. via `.bin/nats` or the Values tab) and the details pane
      immediately shows Name/Subject/Payload Type/Headers/Payload in the specified layout, Ctrl+E
      edits it (Name locked), Ctrl+D deletes it with a confirm prompt, Ctrl+R and `/`/Ctrl+F behave
      per spec. Confirmed 2026-09-02: create/edit/delete/details all behave as specified, including
      the Headers field inside the dialog having no filter/search of its own.
- [x] 6.3 Manually verify Payload Type validation in the dialog: invalid JSON/Base64 disables
      Save with the field flagged; switching Payload Type re-validates the current Payload text
      without requiring a re-type.
