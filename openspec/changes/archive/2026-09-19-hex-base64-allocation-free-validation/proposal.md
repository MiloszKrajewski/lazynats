## Why

`PayloadBinaryText`'s shared decode routine intentionally backs both `PayloadValidation.IsValidHex`/
`IsValidBase64` and `PayloadEncoding.ToBytes`, so validation and encoding can't drift out of sync on
what counts as valid (see `binary-payload-editing-ux`'s design). But the validation call sites -
invoked on every keystroke while composing a Publish/Template dialog's Payload field - still
allocate a full input-sized decoded-byte buffer via `TryDecodeHex`/`TryDecodeBase64`, only to
immediately discard it via `out _`. This was flagged as a low-stakes suggestion during that change's
verification. A chunked-decode approach can eliminate that per-keystroke allocation entirely
without reintroducing the drift risk a hand-rolled charset/padding validator would carry.

## What Changes

- `PayloadBinaryText` gains a validate-only entry point that decodes each already
  boundary-validated (whitespace-stripped, unit-size-aligned) fragment in fixed-size chunks - a
  size that's a common multiple of both the Hex (2) and Base64 (4) unit sizes - into one reused
  `stackalloc` scratch buffer, discarding each chunk's decoded bytes and only checking
  success/failure. Base64 quanta decode independently of neighboring quanta (no cross-quantum
  state), so chunking a fragment this way is byte-for-byte equivalent to decoding it whole; only
  the fragment's own final chunk can legally carry `=` padding, and it still falls at the end of
  that chunk's span.
- `Convert.FromHexString`/`Convert.TryFromBase64Chars` remain the sole decode authority for both
  the new validate-only path and the existing decode-to-array path - no hand-rolled charset or
  padding-position rules - so validation and encoding still cannot disagree on what's valid.
- `PayloadValidation.IsValidHex`/`IsValidBase64` call the new validate-only path instead of the
  existing `TryDecodeHex`/`TryDecodeBase64`.
- `PayloadEncoding.ToBytes`'s `Hex`/`Base64` branches are unchanged - they still need an owned,
  correctly-sized byte array to return, so they keep using `TryDecodeHex`/`TryDecodeBase64` as
  today.
- No change to which payload text is valid or invalid, and no change to any type's encoded bytes -
  this is purely an allocation-strategy change on the validation path.
- The `Payloads/` folder (all seven files: `PayloadType`, `PayloadBinaryText`, `PayloadValidation`,
  `PayloadEncoding`, `PayloadPresentation`, `PayloadContentProbe`, `PayloadContentKind`) moves from
  `src/lazynats` into a new `lazynats.Core` class library project, so this change's own deliverable
  can be exercised by a plain xunit project (`lazynats.Core.Tests`) without constructing Terminal.Gui
  or NATS.Client state. The whole folder moves together rather than splitting it across two
  assemblies, since `PayloadEncoding`/`PayloadValidation`/`PayloadPresentation` already depend on
  each other and on `PayloadBinaryText`/`PayloadType` within the folder. `lazynats.Core` carries no
  Terminal.Gui or NATS.Client package reference (or anything that pulls them in transitively) and is
  built `IsAotCompatible` - the `lazynats` app project references it, not the other way around.
  Namespaces move with the files, from `lazynats.Payloads` to `lazynats.Core.Payloads`, since the
  files now compile into `lazynats.Core`; consumers in `src/lazynats` update their `using`
  directives accordingly.
  `NumericExtensions.cs` (used by `PayloadPresentation`'s Hex/Base64 width clamping) moves alongside
  it from `src/lazynats/Core/` into `lazynats.Core`, since the main app project can no longer be a
  dependency of the library it now depends on.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `payload-types`: gains a new requirement stating that Hex/Base64 validation runs without heap
  allocation proportional to the payload's length - an internal-but-verifiable architectural
  guarantee, not a change to what's valid or how it's encoded, mirroring how `live-feed`'s
  `xxh3-dedup-hash` change stated its dedup key's bit-width as a requirement even though it's not
  independently user-visible. Also gains a requirement that this logic (and the `Payloads/` module
  backing it) lives in the dependency-free `lazynats.Core` library, independently unit-testable.
- `payload-content-probe`, `payload-presentation`: their backing code (`PayloadContentProbe`,
  `PayloadContentKind`, `PayloadPresentation`) relocates into `lazynats.Core` alongside the rest of
  `Payloads/` as part of the same move (no behavior change, so no requirement text changes for
  these two capabilities - see the shared requirement under `payload-types` above).

## Impact

- New `src/lazynats.Core/lazynats.Core.csproj` - a `net10.0`, `IsAotCompatible` class library with
  no Terminal.Gui/NATS.Client dependency, holding the relocated `Payloads/` folder and
  `NumericExtensions.cs`; referenced by `src/lazynats/lazynats.csproj`. `InternalsVisibleTo` covers
  `lazynats` and `lazynats.Core.Tests` so the existing `internal` modifiers on these types need no
  widening.
- New `src/lazynats.Core.Tests/lazynats.Core.Tests.csproj` - an xunit project referencing only
  `lazynats.Core`, covering this change's validate-only decode path (parity with
  `TryDecodeHex`/`TryDecodeBase64`, the padding-suffix edge case, a multi-chunk payload, and the
  no-proportional-allocation guarantee).
- `src/lazynats.sln` gains both new projects.
- `src/lazynats/Payloads/PayloadBinaryText.cs` (now `src/lazynats.Core/Payloads/PayloadBinaryText.cs`)
  - add a validate-only, stackalloc-chunked decode path; existing `TryDecodeHex`/`TryDecodeBase64`
  (used by `PayloadEncoding.ToBytes`) are unchanged.
- `src/lazynats/Payloads/PayloadValidation.cs` (now
  `src/lazynats.Core/Payloads/PayloadValidation.cs`) - `IsValidHex`/`IsValidBase64` route through
  the new validate-only path.
- `PayloadEncoding.cs`, `PayloadPresentation.cs`, `PayloadContentProbe.cs`, `PayloadContentKind.cs`,
  `PayloadType.cs` move file location only (into `lazynats.Core`) - no content changes.
- No content changes to `PublishDialog.cs`, `TemplateDialog.cs`, `TemplatePayloadCodec.cs`, or any
  other `src/lazynats` consumer of `lazynats.Payloads` types, beyond updating their `using` to
  `lazynats.Core.Payloads` - they keep compiling against the same types, now via the new project
  reference.
