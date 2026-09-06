## Context

`lazynats` already has three drill-down management tabs (Streams, Values, Objects) built on
`DrillableListView<T>`/`PollingDetailsView<TTarget, TInfo>`, and one flat list-editor usage today:
`HeaderEditorView` inside `PublishDialog`, built on `ListEditorView<T>`. The Templates tab reuses
the former shape rather than the latter: a `TemplateListView: DrillableListView<Template>` paired
with a `TemplateDetails: PollingDetailsView<Template, Template>`, giving it the same LHS-list/
RHS-details split every other management tab already has, just without a second, drilled-into
level (Templates has nowhere to descend to) - see "List hosting (revised)" below. (An earlier
version of this design instead hosted a tab-scoped `ListEditorView<Template>`, full-width with no
details pane, mirroring `SubscribeTab`; that shape is what `tab-scoped-list-shortcuts`'s
`TabOperations` escape hatch was designed for, and `HeaderEditorView`/`SubscriptionsView` still use
it exactly that way - only the Templates tab itself moved off of it.)

It's also the first place the app persists its *own* structured data in NATS, rather than only
reading/writing user-facing NATS resources. `nats.client.keyvaluestore` 3.2.0's
`INatsKVContext.CreateStoreAsync(NatsKVConfig)` is documented as "Create a new Key Value Store or
get an existing one" (i.e. get-or-create, not create-or-throw) - confirmed via the package's
shipped XML docs - which is the mechanism that makes lazy bucket creation on first write simple
and race-safe, without any catch-and-retry dance.

## Goals / Non-Goals

**Goals:**
- A `5:Templates` tab, flat (no drilling), with the same Ctrl+N/D/E/R/F vocabulary every other
  management tab already offers, per `tab-scoped-list-shortcuts` and `list-editor`.
- Templates persisted as one JSON document per KV entry in `lazynats-templates`, created lazily
  and transparently on the first successful write - never on a read.
- Payload-type-aware validation (JSON must parse; Base64 must decode) enforced client-side before
  Save is enabled, mirroring `nats-publish`'s Send-validation shape.

**Non-Goals:**
- Any "use this template to send/query/import/export" action - explicitly deferred; this change
  only stores and edits templates.
- Template groups/folders (the `invoicing`/`bookings` grouping in `doc/UI.md`'s original sketch)
  or a `hex` payload type - both dropped from that sketch for this first slice; the flat KV-bucket
  model has no natural place for a group axis, and `hex` isn't in this change's requested type set
  (`Json`/`Text`/`Base64`).
- Hiding `lazynats-templates` from the Values tab's bucket list - it stays visible and manageable
  there like any other bucket, matching the "power-user tool" stance already applied elsewhere in
  this app.

## Decisions

**Storage shape: one JSON document per template, keyed by template name.**
The KV key *is* the template name, so the stored document does not repeat it. Document shape:
```json
{ "subject": "orders.get", "headers": {"tenant": "acme"}, "payloadType": "Json", "payload": "{\"id\": 1}" }
```
`payload` is always a JSON *string* value, regardless of `payloadType` - for `Json` this means a
JSON-encoded-JSON string (the Payload field's text, verbatim), not a nested object. This keeps the
document shape uniform across all three payload types and matches the UI: the Payload field is a
single multi-line text area regardless of which type is selected (same as `PublishDialog`'s
Payload field), so there is exactly one string to persist either way. `PayloadType` serializes as
its C# enum name (`Json`/`Text`/`Base64`) via `JsonStringEnumConverter`, for readability if the
bucket is inspected via the Values tab or `nats kv get`.

Alternative considered: storing `payload` as a native JSON value when `PayloadType == Json`
(matching `doc/UI.md`'s original sketch, `"payload": {"id": -1}`). Rejected for this change: it
would make the document shape type-dependent, complicating both serialization (a discriminated
`payload` type) and the edit dialog (which only ever edits one flat string, never a live JSON
value) for no behavior this change needs.

**Serialization: `System.Text.Json` with a source-generated `JsonSerializerContext`.**
No JSON serialization exists in the app layer yet (only inside the NATS client libraries). Given
`PublishAot`, use a `[JsonSerializable]`-attributed source-generated context for the template
document type rather than the reflection-based default, per CLAUDE.md's AOT guidance.

**Bucket creation: lazy, via `CreateStoreAsync` called unconditionally before every write.**
Create/Edit/Delete all call `_kv.CreateStoreAsync(TemplatesBucketConfig)` before their actual
`PutAsync`/`DeleteAsync` call. Since `CreateStoreAsync` is documented as get-or-create, this is
idempotent and safe to call on every write rather than only the first - no bucket-exists check,
no catch-and-create fallback. `TemplatesBucketConfig` is a fixed `NatsKVConfig` (File storage,
otherwise library defaults), analogous to how `nats-kv`'s own Create Bucket requirement fills
unexposed fields with safe explicit defaults.

Reads (the template list fetch, and Delete's list refresh) use `GetStoreAsync` instead, which
does *not* create the bucket - matching "not added on read."

**Missing bucket on read renders as empty, not an error.**
The template-list fetch wraps `GetStoreAsync` + key enumeration in a single try/catch and treats
*any* failure there (bucket missing being the overwhelmingly common case before the first
template is ever saved) as "zero templates," reusing `list-editor`'s existing empty-state hint -
no distinct error path. See Risks/Trade-offs for the resulting false-empty case on a genuine
connectivity failure.

**List hosting (revised): `DrillableListView<Template>` + a details pane, not a tab-hosted
`ListEditorView<Template>`.**
Superseded the original "flat `ListEditorView<Template>`, full-width, no details pane" shape
(mirroring `SubscribeTab`) with the same LHS-list/RHS-details split `StreamsTab`/`ValuesTab`/
`ObjectsTab` already use - a 40%-width list (with an attached `FilterBox` for "/"-search) on the
left, a read-only details pane on the right - just without a second, drilled-into level, since
Templates has nowhere to descend to. `TemplateListView: DrillableListView<Template>` enables
Create/Delete/Edit/Filter (no Descend/Ascend); `TemplatesTab` owns the actual
`lazynats-templates` KV reads/writes/deletes itself and reacts to `CreateRequested`/
`EditRequested`/`DeleteRequested`/`RefreshRequested`/`HighlightChanged`, the same division of
responsibility `StreamsTab` has with `StreamListView`. This also means templates are sorted
alphabetically by name (a side effect of `DrillableListView<T>`'s own sorted-`_items` invariant),
which the original `ListEditorView`-hosted shape didn't do (insertion order).

Reusing `DrillableListView<T>` instead of `ListEditorView<T>` left `ListEditorView<T>`'s
`SelectedItem`/`ReplaceItems` members with no remaining caller (they existed solely to support the
original Templates shape) - removed rather than kept as dead public API.

**Bug fix (found via this work): Enter on a "leaf" list no longer hijacks focus to its search box.**
Templates is the first *flat* (no `EnableAscend`, nothing to descend into) `DrillableListView<T>`
paired with an attached `FilterBox`, and testing it surfaced a pre-existing defect: `EnableDescend`
was strictly opt-in (`_listView.Accepted += (_, _) => DescendRequested?.Invoke();`), so a list that
never calls it leaves Enter's `Command.Accept` genuinely unhandled at the `ListView` level; that
unhandled command then bubbles up through the view hierarchy (Terminal.Gui v2's built-in Accept
propagation - see `command.html`'s `DefaultAcceptHandler`/`DefaultAcceptView` docs) and ends up
moving keyboard focus to the tab's attached `FilterBox` instead of doing nothing. This was already
reachable via the pre-existing Values/Objects key/file lists (also leaf lists with an attached
`FilterBox`) - just apparently unnoticed until Templates' own testing exercised the same path.
Fixed by moving descend wiring off the opt-in `EnableDescend()` method entirely: the constructor
now always does `AddCommand(Command.Accept, () => { DescendRequested?.Invoke(); return true; });`
on the wrapper `DrillableListView<T>` itself (not `_listView`), so it catches Enter's bubbled
Accept command unconditionally and marks it handled - `DescendRequested` simply has no subscriber
on a leaf list, making this a genuine no-op there, while a two-level tab (Streams/Values/Objects)
gets identical descend behavior as before by simply subscribing to `DescendRequested`. See
`drillable-list`'s modified "Shared Descend Wiring" requirement.

**Details pane: `TemplateDetails`, a `PollingDetailsView<Template, Template>` with polling
disabled.**
Unlike every other `PollingDetailsView` subclass, there is nothing to poll: the highlighted list
item already carries the template's full value, so `PollInterval` is overridden to `null` (no
poll pipeline ever starts, consistent with Templates never auto-refreshing anything - see "Manual
Template List Refresh") and `TemplatesTab.OnHighlightChanged` calls `Show(template)` directly with
the already-in-hand value, the same way `BucketDetails`/`StreamDetails` show an already-fetched
list item on highlight change instead of waiting on a fetch. Still built on `PollingDetailsView`
purely to reuse its label:value-rows-plus-body rendering (`OnDrawingContent`) rather than
duplicating it. Layout: `BuildRows` returns Name/Subject/Payload Type (`PollingDetailsView`'s own
renderer already inserts one blank row after the header rows); `BuildBody` returns Headers
(one `key: value` line each) followed by a blank line then Payload, as a single string - so the
full panel reads Name, Subject, Payload Type, (blank), Headers, (blank), Payload, matching the
same shape `KeyDetails` uses for a KV value preview (metadata rows + one combined body string)
rather than separate structured widgets for Headers and Payload.

**Create/Edit dialog: a new `TemplateDialog`, structurally `PublishDialog` + Name + Payload Type.**
Reuses `HeaderEditorView`/`EditFrame`-wrapped Subject and Payload fields exactly as `PublishDialog`
lays them out, adding a Name field above Subject (locked once editing an existing template, per
`CreateKeyDialog`'s Name-on-edit convention) and a Payload Type selector (a `RadioGroup` or
`ComboBox` - implementation detail for tasks.md) between Headers and Payload. Save is disabled
whenever Name is empty, or Payload fails its current Payload Type's validation (JSON parse /
Base64 decode); Text has no payload validation, matching Send's own "only Subject blocks" shape
in `nats-publish`.

**Header editor (revised): `HeaderEditorView` never offers filter/search, in either dialog.**
Realistically well under 10 headers on any message, ever - not worth "/"-search/Ctrl+F chrome. So
this is a permanent property of `HeaderEditorView` itself (no `EnableFilter()` call, and no
`FilterDialogTitle` override), not something `TemplateDialog` alone opts out of: `PublishDialog`'s
own header editor loses its `FilterBox` too, and both dialogs' header `EditFrame` starts right
where its label ends (no filter box row to make room for), with the reclaimed 3 rows folded into
the frame's own height instead - at least 3 lines of content for `TemplateDialog`, 6 for
`PublishDialog` (which had more headroom to begin with).

**Delete confirmation: same modal confirm-prompt shape as Stream/Bucket/Key delete.**
No new pattern - reuses the existing `MessageBox.Query`-based confirm-before-destructive-action
convention.

## Risks / Trade-offs

- [A transient connectivity failure during the very first template-list fetch renders as "no
  templates" rather than an error, since that failure path is intentionally treated the same as
  "bucket doesn't exist yet."] → Acceptable: every other operation on the tab (Ctrl+R, Ctrl+N,
  Ctrl+E, Ctrl+D) still surfaces its own failures normally via the tab's status-bar message, so a
  persistent connectivity problem won't stay silently invisible past the first fetch.
- [Storing `payload` as a JSON-encoded-JSON string for `PayloadType == Json` is a slightly unusual
  shape for anyone inspecting the bucket directly via `nats kv get` or the Values tab.] →
  Documented here and left deliberately uniform rather than type-dependent; a future "Export"
  action (explicitly out of scope) is the more natural place to offer a friendlier on-disk/JSON
  interchange format if ever needed.

## Migration Plan

Purely additive: a new tab, a new folder, a new (lazily-created) KV bucket. No existing capability
changes behavior. No rollback concerns beyond reverting the change.

## Open Questions

None outstanding - the three ambiguities raised during proposal (use/send action, bucket
visibility in Values, Base64 validation) were resolved before this design was written; see
`proposal.md`'s "What Changes" and this file's Decisions for the resolutions.
