## Why

The KV Value store's Create/Edit Key dialog only ever writes a key's value as plain UTF-8 text,
and Edit additionally refuses to open at all for a value that fails a printable-text guard - so a
binary value written by another client (or containing control bytes) can be viewed (via the
existing hex-dump peek and the read-only KV Value Detail dialog) but never edited or recreated
through the UI. Publish and Templates already solve exactly this with a Payload Type selector
(`Json`/`Text`/`Base64`/`Hex`) plus shared client-side validation; the Value store should offer the
same capability instead of a plain-text-only field, so any value - text, JSON, or binary - can be
composed and validated the same way everywhere the app lets you write a payload.

## What Changes

- Add a Payload Type dropdown (`Json`/`Text`/`Base64`/`Hex`, reusing `PayloadType`/
  `PayloadValidation`/`PayloadEncoding` from `lazynats.Core.Payloads`) to the Create/Edit Key
  dialog, between Name and Value, mirroring `TemplateDialog`'s shape.
- The Create/Edit action is enabled only when the selected Payload Type's validation accepts the
  current Value text (same rule `PublishDialog`/`TemplateDialog` already apply), and the value is
  written via `PayloadEncoding.ToBytes` instead of always UTF-8-encoding the raw field text.
- **BREAKING**: Remove the Edit Key printable-text guard. Opening Edit now always fetches the
  key's current entry, classifies it via `payload-content-probe`, and seeds Payload Type/Value from
  that classification (`Json`→`Json` pretty-printed, `Utf8Text`→`Text`, `Binary`→`Hex`, rendered via
  `PayloadPresentation` the same way `TemplateDialog.SeedPayloadText` renders Hex/Base64) instead of
  refusing to open for anything that isn't printable UTF-8 text.
- Create defaults Payload Type to `Text`, matching `PublishDialog`'s default.

## Capabilities

### Modified Capabilities
- `nats-kv`: "Create Key" and "Edit Key" gain a Payload Type selection step and encode/decode the
  Value through it instead of plain UTF-8 text; "Create Key Field Validation" now also gates on
  Payload Type validity; "Edit Key Printable-Text Guard" is removed - Edit is now always reachable
  and seeds Payload Type/Value from the entry's content classification instead of refusing to open.

## Impact

- `src/lazynats/Values/CreateKeyDialog.cs`: add the Payload Type dropdown, validity/encoding wiring.
- `src/lazynats/Values/NewKeyOptions.cs`: carry the selected `PayloadType` alongside Name/Value.
- `src/lazynats/Values/ValuesTab.cs`: `TryCreateKeyAsync`/`TryEditKeyAsync` encode via
  `PayloadEncoding.ToBytes`; `OpenEditKeyDialog`/`TryOpenEditKeyDialogAsync` drop the printable-text
  guard and seed from content classification instead.
- `src/lazynats/Values/ValueText.cs`: removed - superseded by content-classification-based seeding.
- `openspec/specs/nats-kv/spec.md`: requirement updates described above.
