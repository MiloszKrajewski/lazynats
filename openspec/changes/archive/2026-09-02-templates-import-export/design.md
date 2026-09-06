## Context

Templates (`src/lazynats/Templates/`) currently stores each template as a `TemplateDocument`
(Subject/Headers/PayloadType/Payload) under a key equal to the template's Name in the
`lazynats-templates` KV bucket, serialized via a source-generated `TemplateJsonContext`
(`System.Text.Json`, `PublishAot`-safe). `PayloadType` is `Json`/`Text`/`Base64`; the archived
`add-templates-tab` design deliberately made `Payload` a plain string in the document *regardless*
of `PayloadType`, so a `Json`-typed template's payload is stored JSON-encoded-as-a-string
(`"payload": "{\"id\":1}"`) rather than a native JSON value — a decision this change reverses (see
`doc/UI.md`'s original sketch, which always intended `"payload": {"id": -1}` for `json`).

`PublishDialog` (`src/lazynats/Publish/PublishDialog.cs`), by contrast, has no `PayloadType`
concept at all: `Send` always publishes `_payloadView.Text` as-is, relying on
`NatsConnection.PublishAsync`'s default `string` serializer (UTF-8 text). It cannot send
Base64/Hex-decoded binary today.

`ObjectFileDialog` (`src/lazynats/Objects/ObjectFileDialog.cs`) already establishes the pattern
for local-file interaction in this app: Terminal.Gui's built-in `OpenDialog`/`SaveDialog`, run
modally via `App!.Run(picker)`, checked via `picker.Canceled`. Import/Export reuses that pattern
directly rather than introducing a new file-picking mechanism.

## Goals / Non-Goals

**Goals:**
- Fix `Json` payload storage to use a native JSON value on disk, while staying able to read
  templates written under the old string-encoded shape (no forced migration step).
- Add `PayloadType.Hex`, validated and encoded the same way `Base64` already is.
- Share payload-type validation and byte-encoding between Templates and Publish instead of
  duplicating or leaving Publish without any.
- Bulk Export (bucket → local JSON file) and Import (local JSON file → bucket) for the whole
  `lazynats-templates` bucket, driven by native file-picker dialogs.
- Give Publish the same Payload Type concept Templates already has, so Send can actually produce
  binary payloads.

**Non-Goals:**
- Template groups/folders (`doc/UI.md`'s `invoicing`/`bookings` sketch) — the export/import file
  format stays the same flat `name -> document` shape the bucket already uses; grouping remains
  out of scope for this change, same as the original Templates change.
- Per-entry conflict resolution UI during Import (e.g. "skip"/"rename"/"overwrite" prompts) — a
  name collision always overwrites, no prompt.
- A "use this template to send" action — still explicitly deferred, per the original Templates
  change's non-goals; Import/Export operate on the whole bucket, not individual sends.
- True atomic/transactional import across the KV writes themselves — see Risks.

## Decisions

**Shared `payload-types` capability: `PayloadType`, `PayloadValidation`, and a new
`PayloadEncoding`, relocated to `src/lazynats/Payloads/`.**
`PayloadType.cs` and `PayloadValidation.cs` move out of `Templates/` (which no longer owns the
concept exclusively) into a new top-level `Payloads/` folder, alongside a new
`PayloadEncoding.cs`:
```csharp
internal static class PayloadEncoding
{
    public static byte[] ToBytes(PayloadType type, string payload) => type switch
    {
        PayloadType.Base64 => Convert.FromBase64String(payload),
        PayloadType.Hex => Convert.FromHexString(payload),
        _ => Encoding.UTF8.GetBytes(payload), // Json, Text: sent as their own UTF-8 text
    };
}
```
`PayloadValidation.IsValid` gains a `Hex` case using the same try/catch-around-the-real-decode
shape as `Base64` (`Convert.FromHexString`, catching `FormatException`). Both `TemplateDialog` and
the new `PublishDialog` Payload Type field call the same `PayloadValidation.IsValid`/
`PayloadEncoding.ToBytes`; `PublishDialog.Send` now calls `PayloadEncoding.ToBytes` before
publishing instead of passing `_payloadView.Text` straight through.

Alternative considered: leave `PayloadType`/`PayloadValidation` in `Templates/` and have
`Publish/` reference them across the folder boundary. Rejected — CLAUDE.md's "one per top-level
folder" convention treats each folder as a self-contained tab/feature; a shared concept used by
two of them belongs in a shared location (mirroring how `Components/` already holds
cross-tab-reused pieces), not borrowed from one feature folder by another.

**`TemplatePayloadCodec`: converts between the flat UI/edit string and the JSON-document
`Payload` node, per `PayloadType`.**
A new `Templates/TemplatePayloadCodec.cs` (Templates-specific — the document shape is a
Templates concept, unlike validation/encoding) with two directions:
- `ToNode(PayloadType type, string payload) -> JsonNode`: for `Json`, `JsonNode.Parse(payload)`
  (the payload text is already validated JSON at this point); for `Text`/`Base64`/`Hex`,
  `JsonValue.Create(payload)` (a plain JSON string node — unchanged from today's shape for these
  three types).
- `ToText(PayloadType type, JsonNode? node) -> string`: for `Text`/`Base64`/`Hex`,
  `node?.GetValue<string>() ?? ""`; for `Json`, if `node` is a `JsonValue` wrapping a `string`
  (the *old* storage shape, or a hand-written import file using it), return that string directly
  (it's already the raw JSON text); otherwise (`node` is a `JsonObject`/`JsonArray`/non-string
  `JsonValue` — the *new* shape) return `node?.ToJsonString() ?? ""`.

This is what makes reading backward compatible without a migration step (see Migration Plan) and
what both `TemplatesTab`'s KV read/write and the new Import/Export file read/write share, so the
document-shape logic lives in exactly one place.

`TemplateDocument.Payload` changes type from `string` to `JsonNode?`. `System.Text.Json`'s
`JsonNode` family has a built-in, non-reflection-based converter, so it is expected to work inside
the existing source-generated `TemplateJsonContext` without extra attribution — confirmed during
implementation (task item) since this is the change's one AOT-risk spot per CLAUDE.md's guidance
to avoid reflection-dependent patterns; if it does not, a hand-written `JsonConverter<JsonNode>`
registration on the context is the fallback, not a design change.

**Storage/read flow: `TemplatesTab` calls `TemplatePayloadCodec` on both sides of the KV
round-trip.**
`WriteAsync` builds `TemplateDocument` with
`Payload = TemplatePayloadCodec.ToNode(template.PayloadType, template.Payload)` instead of passing
the flat string straight through. `FetchTemplatesAsync` builds `Template` with
`Payload = TemplatePayloadCodec.ToText(document.PayloadType, document.Payload)` instead of
`document.Payload` directly. No other part of `TemplatesTab`/`TemplateDialog`/`TemplateDetails`
changes — they still only ever see the flat string form.

**Export file format: identical shape to the KV document, keyed by name, written as one JSON
object.**
```json
{
  "get-invoice": { "subject": "invoices.get", "headers": {"tenant": "acme"}, "payloadType": "Json", "payload": {"id": -1} },
  "ping": { "subject": "svc.ping", "headers": {}, "payloadType": "Text", "payload": "ping" }
}
```
Reuses `TemplateDocument`/`TemplateJsonContext` directly (a new
`[JsonSerializable(typeof(Dictionary<string, TemplateDocument>))]` entry on the same context) —
Export is "fetch every template, project each to a `TemplateDocument` via
`TemplatePayloadCodec.ToNode`, serialize the dictionary, write to the chosen file." No separate
DTO type for the file format; it *is* the storage document shape, one level up (a dictionary
instead of individual KV entries). This also means Export's on-disk shape and a future `nats kv`
inspection of the bucket read the same way, closing the "unusual on-disk shape" risk the archived
Templates design flagged and deferred to "a future Export action."

**Export: `Ctrl+X` on the Templates tab, `SaveDialog`, whole-bucket, no confirmation prompt for
overwrite (delegated to the picker).**
Fetches the full template list the same way `FetchTemplatesAsync` already does (reused, not
duplicated), builds the dictionary, and writes indented JSON. Terminal.Gui's `SaveDialog` is
expected to prompt for confirmation itself when the chosen path already exists (the same one
`ObjectFileDialog`'s Download mode already relies on for this) — verified during implementation,
not treated as this change's own responsibility to re-implement. Zero templates exports an empty
`{}` object rather than being disabled — harmless, and one less state to special-case.

**Import: `Ctrl+O` on the Templates tab, `OpenDialog` (`MustExist = true`), validate-then-write
(no partial import on failure).**
Reads the file, deserializes as `Dictionary<string, TemplateDocument>`, and for every entry:
name must be non-empty, Subject must be non-empty, and the payload (via
`TemplatePayloadCodec.ToText` then `PayloadValidation.IsValid`) must be valid for its
`PayloadType` — the same three checks "Create Template Field Validation" already enforces per
entry, applied here to the whole set before any write happens. If every entry passes, the system
calls `CreateStoreAsync` once (bucket created if it didn't exist, same lazy-creation convention as
Create/Edit) followed by one `PutAsync` per entry — a name colliding with an existing template
overwrites it, matching `TemplateDialog.Commit`'s own "last one wins on duplicates" stance rather
than prompting per conflict. If any entry fails validation, the whole file is rejected up front (a
single error naming the failing key, before any `PutAsync` is issued) and nothing is written.
After a successful import, the template list is refreshed the same way `WriteAsync` already
refreshes it after a single Create/Edit.

Alternative considered: best-effort import (write the valid entries, report which ones were
skipped). Rejected — a partially-applied bulk operation is harder to reason about than an
all-or-nothing one, and this app has no precedent for a partial-success bulk write; validating
in-memory first is cheap and keeps the mental model simple ("the file was either good or nothing
happened").

**File picking: reuse `OpenDialog`/`SaveDialog` directly, no new dialog type.**
Unlike `ObjectFileDialog` (which pairs a Path field with an editable Key field for Upload/
Download), Import/Export need only a single file path with no other field, so the picker is
invoked directly from `TemplatesTab` (`App!.Run(new OpenDialog { ... })` /
`App!.Run(new SaveDialog { ... })`), same as `ObjectFileDialog.Browse()`'s own call, with no
wrapping `Dialog<T>` subclass. Filtered to `.json` via the picker's `AllowedTypes` (exact API
confirmed via context7's `websites/gui-cs_github_io_terminal_gui` docs during implementation) —
`ObjectFileDialog` doesn't filter (arbitrary object bytes), but Templates' file is always JSON.

**Keyboard: `Ctrl+O` (Import) / `Ctrl+X` (Export), advertised as extra `TemplatesTab`-level
shortcuts, not list-view operations.**
Both act on the whole bucket, not the highlighted template, so — unlike Create/Edit/Delete/Refresh
(dispatched through `TemplateListView.TabOperations` per `tab-scoped-list-shortcuts`) — they are
added directly on `TemplatesTab`: its `OnKeyDownNotHandled` also checks a small fixed
Import/Export table, and its `Shortcuts` property appends their hints to
`_listView.TabOperations`. This mirrors `ObjectListView`'s own precedent for a tab/list-specific
extra operation (`Ctrl+S` Download) that falls outside the shared Create/Delete/Edit/Refresh/
Filter vocabulary `tab-scoped-list-shortcuts` covers — except here the operation belongs to the
tab, not the list, since it has nothing to do with the currently selected row. Neither key is used
elsewhere in the app today.

Import was originally proposed as `Ctrl+I`, changed to `Ctrl+O` ("Open") during implementation:
`Ctrl+<letter>` is computed as that letter's ASCII code with the top three bits masked off, and `I`
masks to `0x09` — the exact same byte the Tab key itself sends. No terminal (this app's own tmux-
based manual test included, per CLAUDE.md) can deliver `Ctrl+I` as a keystroke distinguishable
from Tab, and Tab is already claimed as the universal focus-advance key, so a `Ctrl+I` binding
would never fire for any user. `Ctrl+M` (Enter) has the identical collision and was rejected for
the same reason, with the added risk of turning a plain Enter press on the (currently
Enter-inert) Templates list into a silent bulk-overwrite trigger. `Ctrl+O` has no such collision.

**Publish dialog: adds a Payload Type field between Headers and Payload, identical placement and
widget to `TemplateDialog`'s.**
A `DropDownList<PayloadType>` defaulting to `Text` (preserving today's behavior exactly when left
untouched), wired to `UpdateValidity()` the same way `TemplateDialog`'s dropdown is. `Send`'s only
change: `PayloadEncoding.ToBytes(payloadType, _payloadView.Text)` replaces
`_payloadView.Text` in the `PublishAsync` call, and `UpdateValidity` now also requires
`PayloadValidation.IsValid(payloadType, payload)` alongside the existing non-empty-Subject check
(mirroring `TemplateDialog`'s validity gate, not `PublishDialog`'s current Subject-only gate).
Every other field/behavior (headers, framed layout, Cancel/Send, inline failure reporting) is
unchanged.

Alternative considered: keep Publish payload-type-free and only add typed payload support to
Templates (since the proposal's core ask is Import/Export). Rejected — once `payload-types` is a
shared capability with a real `Hex`/`Base64` encode step, leaving Publish unable to send anything
but raw text is an inconsistency the user explicitly asked to close ("extend publish dialog with
new payload type / payload approach"), and it is a small, mechanical extension once
`TemplateDialog`'s shape already exists to mirror.

## Risks / Trade-offs

- [`Import`'s "validate everything, then write everything" is not truly atomic — a `PutAsync`
  failing partway through a large import (e.g. a mid-import connectivity drop) still leaves some
  entries written and others not, even though validation itself was all-or-nothing.] → Accepted:
  matches this app's existing error-handling depth (no operation anywhere retries or rolls back a
  partially-applied multi-write sequence); the failure is still reported via the same
  `MessageBox.ErrorQuery` path Create/Edit/Delete already use, and a re-run of Import after fixing
  the underlying problem simply overwrites the entries that did land (idempotent, since Import is
  already "overwrite on name collision").
- [`JsonNode` inside a source-generated `JsonSerializerContext` is new to this codebase — the
  existing `TemplateDocument.Payload: string` was chosen partly to avoid needing this.] → Verify
  early during implementation (small standalone check, or via `lazynats.AotProbe` if a full AOT
  publish check is warranted) before building the rest of the change on top of it; the codec
  abstraction (`TemplatePayloadCodec`) keeps the blast radius of a fallback (hand-written
  converter) contained to one file if needed.
- [Changing `TemplateDocument.Payload`'s CLR type from `string` to `JsonNode?` is a breaking
  change to anything reading the bucket's JSON shape outside this app (e.g. `nats kv get` piped
  into another tool expecting the old string-encoded shape for `Json` entries).] → Accepted, since
  the new shape is what `doc/UI.md` always intended and what a faithful Export needs; reads stay
  backward compatible (old string-encoded entries still load correctly via
  `TemplatePayloadCodec.ToText`'s dual handling), so only entries *written* after this change adopt
  the new on-disk shape.

## Migration Plan

Additive/backward-compatible: no bucket migration step. Existing `Json`-typed entries written
under the old string-encoded shape continue to read correctly (`TemplatePayloadCodec.ToText`
detects and unwraps that shape); the first Edit-and-Save of such an entry rewrites it under the
new native-JSON shape going forward. `Text`/`Base64` entries are unaffected (their on-disk shape
was already a plain JSON string and stays that way). No rollback concerns beyond reverting the
change; a bucket written to by both old and new binaries during a rollout stays readable by both,
since old code was never expecting anything but a JSON string in that field's position and new
code handles both.

## Open Questions

- Exact Terminal.Gui v2 API shape for filtering `OpenDialog`/`SaveDialog` to `*.json`
  (`AllowedTypes`, or equivalent) — resolve via context7's
  `websites/gui-cs_github_io_terminal_gui` docs during implementation; if no clean filter API
  exists, fall back to an unfiltered picker (same as `ObjectFileDialog`) rather than blocking on
  it.
