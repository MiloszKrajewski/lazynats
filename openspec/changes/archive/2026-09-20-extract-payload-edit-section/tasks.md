## 1. PayloadEditSection component

- [x] 1.1 Create `src/lazynats/Components/PayloadEditSection.cs`: type label + `DropDownList<PayloadType>`
      (full, unrestricted values) in a fixed-height `EditFrame` band, payload/value label +
      `TextView` in an `EditFrame` filling the section's remaining `Dim.Fill()` height; section's
      own `Width`/`Height` are caller-assigned `Dim` with no internal min/max.
- [x] 1.2 Wire `WordWrap` (off for `Json`, on otherwise) and invalid-scheme highlighting
      (`PayloadValidation.IsValid`) internally, exposing `IsValid` and `Bytes`
      (`PayloadEncoding.ToBytes`, valid only when `IsValid`) plus a changed notification.
- [x] 1.3 Add `SeedFromBytes(byte[] data, PayloadType type)`: `Json`/`Hex`/`Base64` via
      `PayloadPresentation.Render` at the section's own resolved editor width (computed after
      layout, mirroring `PayloadDetailSection`'s two-phase construct+measure pattern); `Text` via
      raw UTF-8 decode.
- [x] 1.4 Add a plain initial `(PayloadType, string)` seeding path (constructor parameters) that
      sets text verbatim, no rendering - used for blank Create and retry-reopen-after-failure.
- [x] 1.5 Accept a caller-supplied label string ("Payload" vs "Value") for the payload/value row.

## 2. Rewire PublishDialog

- [x] 2.1 Replace `PublishDialog`'s standalone Payload Type dropdown + `TextView` + validity wiring
      with a `PayloadEditSection` (label "Payload", blank initial `Text`).
- [x] 2.2 Update `UpdateValidity`/`Send` to read `IsValid`/`Bytes` from the section instead of
      calling `PayloadValidation`/`PayloadEncoding` directly.
- [x] 2.3 Manually verify: Create/send with each Payload Type, invalid text blocks Send, Json
      disables wrap.

## 3. Rewire CreateKeyDialog / ValuesTab

- [x] 3.1 Replace `CreateKeyDialog`'s Payload Type dropdown + Value `TextView` + validity wiring
      with a `PayloadEditSection` (label "Value"), sized via `Dim.Fill()` width and the dialog's
      existing screen-based height clamp, unchanged.
- [x] 3.2 Delete `CreateKeyDialog.SeedValueWidth` and `ValuesTab.SeedValueText`; have
      `ValuesTab.TryOpenEditKeyDialogAsync` pass the fetched entry's raw bytes + classified type to
      the dialog, which forwards them to the section's `SeedFromBytes`.
- [x] 3.3 Keep the retry-reopen-after-failure path (`ValuesTab.TryEditKeyAsync`'s catch branch)
      going through the section's verbatim-text seeding, not `SeedFromBytes`.
- [x] 3.4 Manually verify: Create, Edit on each of a Json/Text/Hex/Binary-classified key (seeded
      per the unified rule), and a failed Edit reopening with exactly what was typed.

## 4. Rewire TemplateDialog

- [x] 4.1 Replace `TemplateDialog`'s Payload Type dropdown + Payload `TextView` + validity wiring
      with a `PayloadEditSection` (label "Payload"), sized to match the dialog's existing `76`x`11`
      literal.
- [x] 4.2 Delete `TemplateDialog.SeedPayloadText`; for Edit, encode the template's stored
      `(PayloadType, string)` to bytes via `PayloadEncoding.ToBytes` and pass those bytes + type to
      the section's `SeedFromBytes` (unifying Json onto the render-from-bytes rule).
- [x] 4.3 Keep Create (blank) and any retry-reopen-after-failure path on the verbatim-text seeding.
- [x] 4.4 Manually verify: Create, Edit on a template with a minified Json payload now reopens
      pretty-printed; Hex/Base64 still render grouped; Text unaffected.

## 5. Spec sync and cleanup

- [x] 5.1 Confirm `openspec/specs/nats-templates/spec.md`'s Edit Template requirement matches this
      change's delta spec once archived (`openspec-sync-specs`/`opsx:sync` or archive).
- [x] 5.2 Grep for any remaining references to the deleted helpers
      (`SeedValueText`, `SeedValueWidth`, `SeedPayloadText`) to confirm none remain.
- [x] 5.3 `dotnet build src/lazynats.sln` clean build.
