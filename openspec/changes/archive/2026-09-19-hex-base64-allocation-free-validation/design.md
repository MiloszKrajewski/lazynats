## Context

`PayloadBinaryText.TryDecode` (added by `binary-payload-editing-ux`, not yet archived) already
splits Hex/Base64 payload text into whitespace-delimited fragments, checks each fragment's length
against its unit size (2 for Hex, 4 for Base64), and decodes each fragment via the span-based
`Convert` overloads into one pre-sized heap buffer. `PayloadValidation.IsValidHex`/`IsValidBase64`
call this same routine and discard the decoded bytes (`out _`) - so every keystroke's validation
pass still allocates a buffer sized to the payload's length, even though only success/failure is
needed. `PayloadEncoding.ToBytes` is the only caller that actually needs the returned array.

## Goals / Non-Goals

**Goals:**
- Eliminate the per-keystroke heap allocation in `IsValidHex`/`IsValidBase64` for any payload
  length, without changing what counts as valid.
- Keep `Convert.FromHexString`/`Convert.TryFromBase64Chars` as the sole decode authority for both
  validation and encoding, so the two can't drift apart on what's valid (the reason the shared
  routine exists in the first place).
- Preserve exact parity with whole-fragment decoding, including Base64's padding placement rules
  (see the padding decision below) - a chunked scan must reject everything a non-chunked
  `Convert.TryFromBase64Chars` call would reject, and accept everything it would accept.

**Non-Goals:**
- No change to `PayloadEncoding.ToBytes` - it still needs an owned, correctly-sized byte array to
  return, so it keeps using the existing heap-allocating `TryDecodeHex`/`TryDecodeBase64`.
- No hand-rolled charset/padding validator replacing `Convert` - considered and rejected in the
  parent conversation for this change, precisely because it would reintroduce the drift risk the
  shared-routine design exists to avoid.
- No change to `PayloadPresentation`, dialogs, or template storage - this change is scoped
  entirely to `PayloadBinaryText`/`PayloadValidation`'s internal allocation strategy.

## Decisions

### Validate-only path decodes in fixed-size chunks into one reused stackalloc buffer
Add `PayloadBinaryText.IsValidHex`/`IsValidBase64` (names chosen to avoid clashing with the
existing `TryDecodeHex`/`TryDecodeBase64`), each: for every whitespace-delimited fragment (same
split as today, same fragment-length-%-unit-size boundary check as today), further split the
fragment into fixed-size chunks of `ChunkChars` characters (last chunk may be shorter) and decode
each chunk via the same `Convert` calls into one `stackalloc byte[ChunkChars / unitSize *
maxBytesPerUnit]` buffer allocated once before the fragment loop and reused across every chunk of
every fragment, discarding each chunk's decoded bytes and only checking the decode call's
success. `ChunkChars` is a shared constant (proposed: 1024) chosen as a multiple of 4 (hence also
of 2), so it never splits a Hex byte pair or Base64 quantum internally - the same boundary
guarantee `binary-payload-editing-ux` already relies on, just applied one level deeper. A
1024-char chunk needs at most 768 bytes of stack space (Base64's 3-bytes-per-4-chars ratio) -
trivial next to a UI thread's default 1MB stack, called only from a keystroke handler, never
recursively.
Alternative considered: `ArrayPool<byte>.Shared.Rent`/`Return`. Rejected - `stackalloc` needs no
lifecycle management (no `try`/`finally` to guarantee `Return`), and the fixed chunk size means
the buffer is always small and bounded regardless of payload length, which a pool doesn't need but
also doesn't get you for free.

### Base64 padding must be a suffix of the whole (whitespace-stripped) text
Chunking a valid Base64 string into independently-decoded pieces is only sound because a quantum's
decoded bytes never depend on neighboring quanta - *except* for where `=` padding is allowed to
appear. `Convert.TryFromBase64Chars` rejects `=` that isn't part of a trailing pad (e.g.
`"QQ==QQ=="` fails as a single decode, since data follows a padding character), but decoding it in
two independent 4-char chunks (`"QQ=="`, `"QQ=="`) would have each chunk succeed on its own -
silently accepting text that whole-string decoding (and thus `PayloadEncoding.ToBytes`) would
reject. To close this gap without abandoning chunking, the validate-only Base64 path additionally
tracks, across the whole fragment scan, whether a `=` has been seen; once seen, any subsequent
non-whitespace character (in the same fragment or a later one) makes the payload invalid,
regardless of what any individual chunk's own decode call reports. This is an O(1)-extra-state
check threaded through the existing scan, not a second pass. Hex has no padding concept, so it
needs no equivalent check - every chunk decodes fully independently of its neighbors.
Alternative considered: only allow chunking when the fragment is short enough that this can't
happen (i.e. never split a fragment that contains `=` at all). Rejected - a single very long
Base64 fragment that happens to end in `=` would then fall back to one large stackalloc sized to
the whole fragment, reintroducing the same proportional-allocation problem (on the stack instead
of the heap, but with actual overflow risk) for exactly the payloads this change targets.

### `IsValidHex`/`IsValidBase64` still delegate boundary/format decisions to `Convert`
The per-chunk decode call is still `Convert.FromHexString`/`Convert.TryFromBase64Chars` - the new
code only decides *how much* text to feed each call and *where* to write the (discarded) output,
never re-implements what makes a chunk's content valid. This preserves the existing "one shared
decode routine backs both validation and encoding" property from `binary-payload-editing-ux`: the
only new code is the chunking/looping wrapper, not new decode logic.

### `Payloads/` moves whole into a new, dependency-free `lazynats.Core` project
This change's own deliverable (the chunked validate-only decode path) needs to be exercised by
plain xunit tests, without constructing Terminal.Gui or NATS.Client state - `PayloadBinaryText`'s
existing home, `src/lazynats`, references both. Rather than split `Payloads/` across two assemblies
(the two files this change touches vs. the other five), the whole folder - `PayloadType`,
`PayloadBinaryText`, `PayloadValidation`, `PayloadEncoding`, `PayloadPresentation`,
`PayloadContentProbe`, `PayloadContentKind` - moves into a new `lazynats.Core` class library
project, since those seven files already depend on each other within the folder (e.g.
`PayloadEncoding`/`PayloadValidation` call `PayloadBinaryText`; `PayloadValidation` switches on
`PayloadType`) and are otherwise already framework-agnostic (`System.Text.Json`/`System.Buffers`/
`System.Text` only). `NumericExtensions.cs` (the one thing `Payloads/` reaches out of the folder
for, via `PayloadPresentation`'s Hex/Base64 width clamping) moves with it, since `lazynats.Core`
cannot depend back on the `lazynats` app project that will now depend on it. `lazynats.Core` is
built `IsAotCompatible` (implies `IsTrimmable` plus the trim/AOT analyzers) and deliberately carries
no Terminal.Gui or NATS.Client package reference - a project-level guarantee that this module stays
independent of the UI/network stack, not just true by accident today. The `Payloads/` namespace
moves with the files, from `lazynats.Payloads` to `lazynats.Core.Payloads` (mirroring the project
rename); `NumericExtensions.cs` moves into the existing `lazynats.Core` namespace. Consumers in
`src/lazynats` update their `using` directives to match.
Existing `internal` modifiers on these types are kept as-is rather than widened to `public`:
`lazynats.Core.csproj` declares `InternalsVisibleTo` for both `lazynats` (so the app keeps
compiling against these types) and `lazynats.Core.Tests` (so tests can reach them directly), which
is a normal use of `InternalsVisibleTo` for a private, unpackaged library with exactly one
non-test consumer (`IsPackable` stays `false`, so this isn't published as a NuGet package where a
public API surface would matter).
Alternative considered: move only `PayloadBinaryText.cs`/`PayloadValidation.cs` (the two files this
change's proposal originally scoped). Rejected - `PayloadEncoding.ToBytes` already calls
`PayloadBinaryText.TryDecodeHex`/`TryDecodeBase64`, and `PayloadValidation.IsValid` already
switches on `PayloadType`, so a two-file move would still force `PayloadEncoding.cs`/`PayloadType.cs`
across the assembly boundary anyway; splitting the remaining three files (`PayloadPresentation`,
`PayloadContentProbe`, `PayloadContentKind`) off into a separate assembly from their own sibling
`PayloadType`/`PayloadContentKind` dependencies would fragment one cohesive, already-independent
folder for no benefit.

## Risks / Trade-offs

- **[Risk]** The Base64 padding-suffix check is easy to get wrong in a way that only manifests on
  adversarial or unusual input (e.g. `=` appearing mid-fragment), which typing-driven manual
  testing is unlikely to surface. → **Mitigation**: `lazynats.Core.Tests` (see "`Payloads/` moves
  whole into a new, dependency-free `lazynats.Core` project" above) has explicit xunit scenarios
  for it - both a same-fragment case (`"QQ==QQ=="`) and, more importantly, a case built to hide the
  problem at a `ChunkChars` boundary (padding ending exactly at char 1024, more valid-looking data
  in the next chunk) - see `specs/payload-types/spec.md`'s "Validation and encoding still agree on
  validity" scenario.
- **[Risk]** Moving `Payloads/` into a separate assembly and widening its types' reach via
  `InternalsVisibleTo` (rather than `public`) means a future new type added to the folder is
  `internal` and visible to `lazynats`/`lazynats.Core.Tests` by default, which could let something
  not meant for cross-project use leak across without a compiler error to catch it. →
  **Mitigation**: this mirrors the repository's existing `$(AssemblyName).Tests`
  `InternalsVisibleTo` convention in `Directory.Build.props`, just made explicit here since
  `lazynats.Core` isn't `IsPackable`; no different in kind from the risk any `internal` API already
  carries within a single assembly.
- **[Risk]** `ChunkChars` picked too small makes validation slower (more `Convert` calls) for large
  pastes; picked too large risks stack pressure in deeply-nested call stacks. → **Mitigation**:
  1024 is small relative to a 1MB default thread stack and large relative to realistic
  keystroke-driven payload sizes, so it sits comfortably away from either failure mode; revisit
  only if profiling says otherwise.
- **[Risk]** Two decode paths (`TryDecodeHex`/`TryDecodeBase64` for `ToBytes`, the new chunked ones
  for `IsValid*`) could drift in what they accept if someone edits one without the other. →
  **Mitigation**: both still bottom out in the same `Convert` calls with the same fragment-boundary
  pre-check: the only behavioral surface that could drift is the new padding-suffix check, which
  is additive (only rejects, never accepts, something `Convert` itself would accept) - see the
  design decision above for why it can't cause `IsValid` to accept what `ToBytes` then rejects.

## Migration Plan

No data or behavior migration - existing valid/invalid payloads remain exactly as valid/invalid as
before; templates already stored are unaffected. Purely an internal allocation-strategy change
behind `PayloadValidation.IsValidHex`/`IsValidBase64`.

## Open Questions

None - `ChunkChars = 1024` is a reasonable default settled here; revisit only if a future profiling
pass shows it matters.
