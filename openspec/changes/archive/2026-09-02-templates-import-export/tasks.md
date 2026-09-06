## 1. Shared `payload-types` foundation

- [x] 1.1 Create `src/lazynats/Payloads/` and move `PayloadType.cs` there from `src/lazynats/Templates/`, adding `Hex` to the enum (`Json, Text, Base64, Hex`).
- [x] 1.2 Move `PayloadValidation.cs` into `src/lazynats/Payloads/`, add the `Hex` case (`Convert.FromHexString` in a try/catch, mirroring the existing `IsValidBase64` shape), and update its namespace/usages.
- [x] 1.3 Add `src/lazynats/Payloads/PayloadEncoding.cs` with `ToBytes(PayloadType, string) -> byte[]` (UTF-8 for `Json`/`Text`, `Convert.FromBase64String` for `Base64`, `Convert.FromHexString` for `Hex`), per design.md.
- [x] 1.4 Update all existing references to `PayloadType`/`PayloadValidation` (`Templates/TemplateDocument.cs`, `Templates/TemplateDialog.cs`, `Templates/TemplateDetails.cs`) to the new `Payloads` namespace.

## 2. Native JSON payload storage for Templates

- [x] 2.1 Add `src/lazynats/Templates/TemplatePayloadCodec.cs` with `ToNode(PayloadType, string) -> JsonNode` and `ToText(PayloadType, JsonNode?) -> string`, including the backward-compatible `Json`-as-string-node unwrap in `ToText` (design.md's "TemplatePayloadCodec" decision).
- [x] 2.2 Change `TemplateDocument.Payload` from `string` to `JsonNode?`; confirm (small standalone check or a build) that the source-generated `TemplateJsonContext` serializes/deserializes a `JsonNode?` property correctly without reflection; fall back to a hand-written `JsonConverter<JsonNode>` registration only if it doesn't.
- [x] 2.3 Update `TemplatesTab.WriteAsync` to build `TemplateDocument.Payload` via `TemplatePayloadCodec.ToNode`.
- [x] 2.4 Update `TemplatesTab.FetchTemplatesAsync` to build `Template.Payload` via `TemplatePayloadCodec.ToText`.
- [x] 2.5 Manually verify (via `nats kv get lazynats-templates <key>` or the Values tab) that a newly created `Json`-typed template stores a native JSON value, and that a template written by the pre-change binary (string-encoded payload) still loads and displays correctly.

## 3. Templates Export

- [x] 3.1 Add `[JsonSerializable(typeof(Dictionary<string, TemplateDocument>))]` to `TemplateJsonContext`.
- [x] 3.2 Add an Export operation on `TemplatesTab`: reuse `FetchTemplatesAsync`, project each `Template` to a `TemplateDocument` via `TemplatePayloadCodec.ToNode`, serialize the resulting dictionary (indented) via the shared context.
- [x] 3.3 Wire Export to Ctrl+X: open a `SaveDialog` (check context7's `websites/gui-cs_github_io_terminal_gui` docs for `AllowedTypes`/equivalent extension filtering to `*.json`; fall back to unfiltered if no clean API exists), write the file on confirm, report a failure via `MessageBox.ErrorQuery` matching Create/Edit/Delete's existing pattern.
- [x] 3.4 Add the Ctrl+X hint to `TemplatesTab.Shortcuts` (appended alongside `_listView.TabOperations`) and dispatch it from `OnKeyDownNotHandled`.

## 4. Templates Import

- [x] 4.1 Add an Import operation on `TemplatesTab`: open an `OpenDialog` (`MustExist = true`, same `*.json` filtering as Export where available) on Ctrl+O.
- [x] 4.2 On confirm, read and deserialize the file as `Dictionary<string, TemplateDocument>`; on a JSON/shape parse failure, show `MessageBox.ErrorQuery` and stop (no writes).
- [x] 4.3 Validate every entry up front: non-empty name, non-empty Subject, and `PayloadValidation.IsValid(entry.PayloadType, TemplatePayloadCodec.ToText(entry.PayloadType, entry.Payload))`; on the first failing entry, show `MessageBox.ErrorQuery` naming it and stop (no writes).
- [x] 4.4 On full validation success, call `CreateStoreAsync` once then `PutAsync` per entry (overwriting any existing template with the same name), then refresh the template list the same way `WriteAsync` does after a Create/Edit.
- [x] 4.5 Add the Ctrl+O hint to `TemplatesTab.Shortcuts` and dispatch it from `OnKeyDownNotHandled`.

## 5. Publish dialog Payload Type

- [x] 5.1 Add a `DropDownList<PayloadType>` field to `PublishDialog`, positioned between Headers and Payload (same layout/width convention `TemplateDialog` uses), defaulting to `Text`.
- [x] 5.2 Update `PublishDialog.UpdateValidity` to also require `PayloadValidation.IsValid(payloadType, payload)`, flagging the Payload field invalid the same way Subject is flagged today.
- [x] 5.3 Update `PublishDialog.Send`/`PublishAsync` to encode the payload via `PayloadEncoding.ToBytes(payloadType, payload)` and publish the resulting bytes instead of the raw string; confirm the `NatsConnection.PublishAsync` overload used accepts a `byte[]` payload (check context7's `nats-io/nats.net` docs if the current `string`-based overload doesn't have an obvious byte-array sibling).

## 6. Verification

- [x] 6.1 `dotnet build src/lazynats.sln` succeeds.
- [x] 6.2 Drive the app via tmux (per CLAUDE.md's TUI-testing guidance) against a local NATS server: create a `Json`-typed template, Export, inspect the file's `payload` shape, edit the file, Import it back, and confirm the edited template appears correctly (including a deliberately-invalid entry to confirm the whole import is rejected).
- [x] 6.3 Drive the app via tmux to confirm Publish (Alt+P) sends correctly for each Payload Type, including that an invalid Base64/Hex payload disables Send.
- [x] 6.4 Update `CLAUDE.md`'s Architecture bullet list to mention the new shared `Payloads/` folder alongside `Components/`.
