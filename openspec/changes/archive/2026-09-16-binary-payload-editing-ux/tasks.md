## 1. Shared whitespace-tolerant Hex/Base64 decoding

- [x] 1.1 Add a new internal static class in `src/lazynats/Payloads/` (e.g.
      `PayloadBinaryText.cs`) exposing `TryDecodeHex(ReadOnlySpan<char>, out byte[])` and
      `TryDecodeBase64(ReadOnlySpan<char>, out byte[])`: build a `SearchValues<char>` over a fixed
      whitespace set (space, tab, `\r`, `\n`), split the input into non-whitespace fragments,
      require each fragment's length be a multiple of the type's unit size (2 for Hex, 4 for
      Base64), and decode each fragment via the span-based `Convert` overloads
      (`Convert.FromHexString(ReadOnlySpan<char>)`, `Convert.TryFromBase64Chars`) into a single
      pre-sized output buffer (size computed up front from the summed fragment lengths). Return
      `false` on any fragment-length mismatch or decode failure, with no partial/intermediate
      string allocation.
- [x] 1.2 Update `PayloadValidation.IsValidHex`/`IsValidBase64` to call the shared decoder and
      return its success/failure result.
- [x] 1.3 Update `PayloadEncoding.ToBytes`'s `Hex`/`Base64` branches to call the same shared
      decoder and return its decoded bytes.

## 2. Payload field word-wrap

- [x] 2.1 In `PublishDialog`, set `_payloadView.WordWrap` from the Payload Type dropdown's current
      value (`true` for `Hex`/`Base64`/`Text`, `false` for `Json`) both at construction and inside
      the dropdown's existing `ValueChanged` handler, alongside the existing `UpdateValidity` call.
- [x] 2.2 Apply the same change in `TemplateDialog` (construction + the Payload Type dropdown's
      `ValueChanged` handler).

## 3. Normalize Hex/Base64 payload text on template save

- [x] 3.1 Update `TemplatePayloadCodec.ToNode` to normalize `Hex`/`Base64` payload text before
      wrapping it as a JSON string: decode via `PayloadEncoding.ToBytes(type, payload)`, then
      re-encode the decoded bytes via `Convert.ToHexString`/`Convert.ToBase64String` and wrap
      *that* string. `Json`/`Text` are unaffected.

## 4. Restore formatted Hex/Base64 payload on template edit-open

- [x] 4.1 In `TemplateDialog`'s constructor, when `initial` is non-null and its `PayloadType` is
      `Hex` or `Base64`, seed `_payloadView.Text` via
      `PayloadPresentation.Render(PayloadEncoding.ToBytes(initial.PayloadType, initial.Payload), initial.PayloadType, FieldWidth)`
      instead of `initial.Payload` directly, so the field opens wrapped/grouped for readability.
      `Json`/`Text` seeding is unchanged.

## 5. Verification

- [x] 5.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 5.2 Drive the app via `tmux` (per `CLAUDE.md`'s UI-testing pattern) to confirm: space-grouped
      hex and multi-line/wrapped base64 validate and send correctly from the Publish dialog;
      whitespace that splits a byte pair/quantum is still flagged invalid; switching Payload Type
      toggles wrapping live without reopening the dialog; creating a `Hex`/`Base64` template with
      whitespace-formatted payload text, then reopening it for edit, shows the normalized/
      re-rendered form rather than the original raw input.
- [x] 5.3 Spot-check `TemplatesTab`'s import path (`TemplatesTab.cs`, `PayloadValidation.IsValid`
      call) still accepts whitespace-formatted `Hex`/`Base64` payloads in an imported JSON file.
