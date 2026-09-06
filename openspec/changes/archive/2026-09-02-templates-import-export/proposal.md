## Why

The Templates tab (`5:Templates`) can only be populated one entry at a time by hand, with no way
to back up, share, or bulk-load a set of templates across environments. Two other gaps make that
worse: the `Json` payload type is currently persisted as a JSON-encoded *string* inside the KV
document (`"payload": "{\"id\":1}"`) rather than a native JSON value (`"payload": {"id":1}`), which
is both a fidelity bug and an awkward on-disk shape for any future import/export file to inherit;
and there is no `Hex` payload type, even though binary payloads are sometimes easier to type/read
as hex than base64. The Publish dialog (Alt+P) has no payload type concept at all — it always
sends the entered text as raw UTF-8, so it can't send base64/hex-decoded binary either. This
change adds Import/Export to Templates and fixes the payload-type gaps that a faithful,
human-editable interchange format needs first.

## What Changes

- Fix Template Storage: a `Json`-typed template's `payload` is now persisted (and read back) as a
  native JSON value in the `lazynats-templates` KV document, not a JSON-encoded string. Reading
  stays backward compatible with entries written under the old string-encoded shape.
- Add `PayloadType.Hex`: a fourth payload type (`Json`/`Text`/`Base64`/`Hex`), validated as a hex
  string client-side and decoded to raw bytes when the payload is actually sent/stored as bytes.
- Extract payload-type validation and byte-encoding into a new, shared `payload-types` capability,
  reused by both Templates and Publish instead of being duplicated or Templates-only.
- Add Export Templates (Ctrl+X on the Templates tab): writes every template currently in
  `lazynats-templates` to a local JSON file the user picks via a native file-save dialog, as a
  single object keyed by template name, each value carrying `subject`/`headers`/`payloadType`/
  `payload` (payload encoded per its type, per the storage fix above).
- Add Import Templates (Ctrl+O on the Templates tab): reads that same JSON shape from a local file
  the user picks via a native file-open dialog, validates every entry up front, and writes them
  into `lazynats-templates` (creating the bucket if needed). An entry with a name matching an
  existing template overwrites it. If any entry fails validation, nothing is imported and the
  failure is reported.
- Extend the Publish dialog (Alt+P) with a Payload Type selector (`Json`/`Text`/`Base64`/`Hex`,
  defaulting to `Text`) placed between Headers and Payload, matching the Templates dialog. Send
  now validates the payload against the selected type and encodes it accordingly (JSON/Text as
  UTF-8 text, Base64/Hex decoded to bytes) instead of always sending raw UTF-8 text.

## Capabilities

### New Capabilities
- `payload-types`: the shared `Json`/`Text`/`Base64`/`Hex` payload type set — per-type client-side
  validation rules and per-type byte-encoding rules — reused by both Templates and Publish.

### Modified Capabilities
- `nats-templates`: Payload Type gains `Hex`; `Json` payload storage becomes a native JSON value
  instead of a JSON-encoded string; new Export Templates and Import Templates requirements.
- `nats-publish`: the Publish dialog gains a Payload Type field; Send Validation and Send
  Publishes the Message now depend on the selected Payload Type instead of always treating the
  payload as raw UTF-8 text.

## Impact

- `src/lazynats/Templates/`: `PayloadType.cs`/`PayloadValidation.cs` move into a new shared
  `src/lazynats/Payloads/` folder (alongside a new byte-encoding helper); `TemplateDocument.cs`,
  `TemplatesTab.cs`, `TemplateDialog.cs` change to support native JSON payload storage, `Hex`, and
  the new Import/Export operations.
- `src/lazynats/Publish/PublishDialog.cs`: gains a Payload Type field and per-type send encoding.
- `lazynats-templates` KV bucket: `Json`-typed entries' on-disk `payload` shape changes going
  forward (old entries remain readable).
- No new third-party dependencies; file picking reuses Terminal.Gui's built-in `OpenDialog`/
  `SaveDialog` (already used by `ObjectFileDialog`).
