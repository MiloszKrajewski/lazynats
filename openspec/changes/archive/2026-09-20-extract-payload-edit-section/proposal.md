## Why

`PublishDialog`, `TemplateDialog`, and `CreateKeyDialog` each hand-roll the same Payload
Type/Payload(Value) editing UI independently: a `DropDownList<PayloadType>` in an `EditFrame`, a
`TextView` in an `EditFrame` with `WordWrap` toggled off for `Json`, validity/invalid-scheme
wiring against `PayloadValidation`, and commit-time `PayloadEncoding.ToBytes`. The one thing that
differs between call sites — how to seed the field's initial text from an existing payload — has
already drifted: `TemplateDialog.SeedPayloadText` preserves a template's stored `Json`/`Text`
verbatim and only re-renders `Hex`/`Base64`, while `ValuesTab.SeedValueText` always renders from
raw bytes (including a `Text`-specific carve-out to dodge a `PayloadPresentation`
fixed-width-chop/`TextView` `WordWrap` double-wrap bug) — a fix made once, in one call site, that
the other never received. The read-only counterpart of this exact shape (byte payload, selectable
`PayloadType`, adaptive-width render) was already extracted once, as `PayloadDetailSection`,
shared by `MessageDetailDialog` and the KV Value Detail dialog. The write side never got the same
treatment.

## What Changes

- Add `PayloadEditSection`, a `Components/` view mirroring `PayloadDetailSection`'s role for the
  editable side: an editable `PayloadType` dropdown row (fixed height) above a `TextView` payload
  editor row that fills the rest of whatever `Width`/`Height` (`Dim`) the section is given, with no
  internal minimum/maximum of its own — height/width clamping (e.g. `CreateKeyDialog`'s
  screen-size-based cap) stays entirely the caller's responsibility, computed before construction
  exactly as today.
- The section owns: the type dropdown and payload `TextView`, `WordWrap` off only for `Json`,
  invalid-scheme highlighting driven by `PayloadValidation.IsValid`, and a single seeding entry
  point that renders initial bytes to display text per `PayloadType` (`Json`/`Hex`/`Base64` via
  `PayloadPresentation.Render`, `Text` via a raw UTF-8 decode to avoid the double-wrap bug) — one
  rule, used by every caller.
- **BREAKING (internal only)**: `TemplateDialog`'s Edit Template payload seeding changes for
  `Json` — a template's payload now always reopens re-serialized/pretty-printed (matching
  `nats-kv`'s existing Edit Key behavior and the read-only Message Detail default) instead of
  showing exactly the text last saved. `Text` seeding is textually unaffected (UTF-8 round-trips
  losslessly either way). `Hex`/`Base64` seeding is unaffected (already rendered from bytes).
- `PublishDialog`, `TemplateDialog`, and `CreateKeyDialog` are rewired to compose
  `PayloadEditSection` instead of their own duplicated dropdown/`TextView`/validity wiring, reading
  `IsValid`/`Bytes` from it instead of re-deriving them locally.

## Capabilities

### New Capabilities
- `payload-edit-section`: the shared editable Payload Type + payload/value editing view — its
  layout (fixed type row, fill-the-rest editor row), validity highlighting, `WordWrap` behavior,
  and the one seeding-from-bytes rendering rule used by every payload-editing dialog.

### Modified Capabilities
- `nats-templates`: Edit Template's payload-seeding requirement changes from "for `Json`/`Text`,
  the Payload field is seeded with the stored text unchanged" to seeding `Json` by rendering the
  bytes (pretty-printed) and `Text` by decoding the raw bytes — the same rule `nats-kv` already
  documents for Edit Key.

## Impact

- New file: `src/lazynats/Components/PayloadEditSection.cs`.
- Modified: `src/lazynats/Publish/PublishDialog.cs`, `src/lazynats/Templates/TemplateDialog.cs`,
  `src/lazynats/Values/CreateKeyDialog.cs`, `src/lazynats/Values/ValuesTab.cs` (drops
  `SeedValueText`, now redundant with the section's own seeding).
  `TemplateDialog.SeedPayloadText` is removed; seeding moves into `PayloadEditSection`.
- No changes to `lazynats.Core/Payloads/*` — this extraction is purely at the Terminal.Gui
  component layer, reusing the existing `PayloadType`/`PayloadValidation`/`PayloadEncoding`/
  `PayloadPresentation` logic as-is.
- `openspec/specs/nats-templates/spec.md`: Edit Template's payload-seeding scenarios updated to
  match the new rule.
