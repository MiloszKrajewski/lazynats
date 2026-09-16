## Why

Publish and Template dialogs already let the user pick `Hex`/`Base64` as a Payload Type, but the
payload field treats that text as a strict encoded string: no whitespace tolerated, no wrapping, so
a long unbroken string requires horizontal scrolling to edit, and pasting a real-world formatted
blob (space-grouped hex bytes, PEM-style line-wrapped base64) fails validation even though it
represents perfectly valid data — including hex copied straight out of this app's own Message
Detail view, which renders it space-grouped. This makes the existing Hex/Base64 modes error-prone
and unpleasant to use for anything beyond a short unbroken string.

## What Changes

- The Payload field in the Publish and Template dialogs word-wraps when Payload Type is `Hex`,
  `Base64`, or `Text` — but not `Json`, since pretty-printed JSON already has deliberate line
  structure that an added soft-wrap would visually clash with.
- `Hex`/`Base64` validation and encoding become whitespace-tolerant: whitespace is allowed only
  between complete encoding units (a 2-hex-digit byte pair for `Hex`, a 4-character quantum for
  `Base64`) and is discarded before decoding. Whitespace that splits a unit (e.g. a space between
  the two digits of a byte pair) still makes the payload invalid — this isn't "strip all
  whitespace and hope," it's a boundary check that also happens to make the no-allocation
  span-based implementation possible (split on whitespace runs via `SearchValues<char>`, validate
  and decode each fragment's length against its unit size, no intermediate stripped string).
- `Json`/`Text` are unaffected: whitespace stays exactly as typed, since both encode as the literal
  UTF-8 bytes of the payload text — unlike `Hex`/`Base64`, whitespace there is part of the payload,
  not incidental formatting of an encoded representation.
- A saved template with Payload Type `Hex`/`Base64` stores the normalized (whitespace-stripped)
  canonical form, not whatever whitespace the user happened to type or paste.
- Reopening a `Hex`/`Base64` template for editing re-renders its stored bytes through the existing
  `PayloadPresentation.Render`, at the dialog's own field width, so the payload field starts nicely
  wrapped/grouped instead of as one unbroken string.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `payload-types`: Payload Validation and Payload Byte Encoding requirements gain
  whitespace-tolerant, boundary-checked handling for `Hex` and `Base64`; `Json`/`Text` are
  explicitly called out as unaffected.
- `nats-publish`: the Payload field word-wraps for `Hex`/`Base64`/`Text` (not `Json`).
- `nats-templates`: the Create/Edit Template dialog's Payload field word-wraps the same way;
  stored `Hex`/`Base64` payloads are normalized on save; the Edit dialog re-formats a `Hex`/`Base64`
  payload for display when it opens.

## Impact

- `src/lazynats/Payloads/PayloadValidation.cs`, `PayloadEncoding.cs` — whitespace/boundary handling
  for `Hex`/`Base64`, likely sharing a small span-scanning helper between validate and encode
  rather than duplicating the fragment-splitting logic.
- `src/lazynats/Publish/PublishDialog.cs`, `src/lazynats/Templates/TemplateDialog.cs` — payload
  `TextView.WordWrap` toggled by Payload Type; `TemplateDialog` additionally normalizes on
  commit and re-renders via `PayloadPresentation.Render` when seeding from an existing template.
- `openspec/specs/payload-types/spec.md`, `openspec/specs/nats-publish/spec.md`,
  `openspec/specs/nats-templates/spec.md` — requirement updates per Capabilities above.
