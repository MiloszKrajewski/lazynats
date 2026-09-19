## 1. Extract `Payloads/` into a new, dependency-free `lazynats.Core` project

- [x] 1.1 Create `src/lazynats.Core/lazynats.Core.csproj`: `net10.0`, `ImplicitUsings`/`Nullable`
      enabled, `IsAotCompatible=true`, no Terminal.Gui/NATS.Client package reference (or anything
      that pulls them in transitively). `InternalsVisibleTo` covers `lazynats` and
      `lazynats.Core.Tests`, since the folder's types stay `internal`.
- [x] 1.2 Move `src/lazynats/Payloads/*.cs` (all seven files) to `src/lazynats.Core/Payloads/*.cs`
      and `src/lazynats/Core/NumericExtensions.cs` to `src/lazynats.Core/NumericExtensions.cs`, via
      `git mv` (preserve history). Namespaces (`lazynats.Payloads`, `lazynats.Core`) are unchanged.
- [x] 1.3 Add a `ProjectReference` from `src/lazynats/lazynats.csproj` to
      `src/lazynats.Core/lazynats.Core.csproj`.
- [x] 1.4 Create `src/lazynats.Core.Tests/lazynats.Core.Tests.csproj` (xunit, `ProjectReference` to
      `lazynats.Core` only) and add both new projects to `src/lazynats.sln`.
- [x] 1.5 `dotnet build src/lazynats.sln` compiles cleanly with the new project layout, and
      `dotnet list src/lazynats.Core/lazynats.Core.csproj package` shows no Terminal.Gui/NATS.Client
      package.

## 2. Allocation-free validate-only decode path

- [x] 2.1 In `PayloadBinaryText.cs`, add a `private const int ChunkChars = 1024;` and a
      validate-only decode helper parameterized the same way `TryDecode` is (`unitSize`,
      `maxBytesPerUnit`, a `FragmentDecoder`), but instead of writing into a pre-sized heap buffer,
      allocate one `stackalloc byte[ChunkChars / unitSize * maxBytesPerUnit]` before the fragment
      loop and reuse it across every chunk of every fragment, discarding each chunk's decoded
      bytes and returning `false` on the first decode failure or fragment-length-boundary mismatch
      (same boundary check as `TryDecode`'s first pass).
- [x] 2.2 Within that helper, chunk each fragment into `ChunkChars`-sized pieces (last piece may be
      shorter; both are guaranteed multiples of `unitSize` since `ChunkChars % unitSize == 0` and
      the fragment's own length is already a multiple of `unitSize`), decoding each piece with the
      same `FragmentDecoder` delegate `TryDecode` uses.
- [x] 2.3 For the Base64 instantiation specifically, thread a `seenPadding` boolean across the
      whole scan (both across chunks within a fragment and across fragments): once a `=` character
      is seen anywhere, any later non-whitespace character anywhere in the remaining text makes the
      payload invalid, regardless of what any individual chunk's own decode call reports. Hex's
      instantiation needs no equivalent flag.
- [x] 2.4 Expose these as `PayloadBinaryText.IsValidHex(ReadOnlySpan<char>)` and
      `IsValidBase64(ReadOnlySpan<char>)` returning `bool`, distinct from the existing
      `TryDecodeHex`/`TryDecodeBase64` (which `PayloadEncoding.ToBytes` keeps using unchanged).

## 3. Wire validation to the new path

- [x] 3.1 Update `PayloadValidation.IsValidHex`/`IsValidBase64` to call
      `PayloadBinaryText.IsValidHex`/`IsValidBase64` instead of `TryDecodeHex`/`TryDecodeBase64`.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 4.2 Add `lazynats.Core.Tests` coverage confirming existing behavior is unchanged: parity
      between `IsValidHex`/`IsValidBase64` and `TryDecodeHex`/`TryDecodeBase64` across
      whitespace-tolerant valid Hex/Base64, boundary-splitting whitespace, and invalid hex/base64
      text (`PayloadBinaryTextTests.IsValidHex_MatchesTryDecodeHex`/
      `IsValidBase64_MatchesTryDecodeBase64`).
- [x] 4.3 Add a `lazynats.Core.Tests` case for the Base64 padding-suffix edge case: a payload text
      shaped like `"QQ==QQ=="` (padding followed by more non-whitespace data, single fragment) is
      rejected by `IsValidBase64`, matching `TryDecodeBase64`
      (`IsValidBase64_RejectsPaddingFollowedByMoreDataInTheSameFragment`). Also add a case that
      specifically hides the problem at a `ChunkChars` boundary - padding ending exactly at char
      1024, with more valid-looking data in the next chunk, so each chunk would decode fine on its
      own without the `seenPadding` tracking
      (`IsValidBase64_RejectsPaddingHiddenAtAChunkBoundary`).
- [x] 4.4 Add a `lazynats.Core.Tests` case for a multi-kilobyte whitespace-formatted Hex and Base64
      payload (spanning multiple `ChunkChars` chunks) that validates correctly and decodes to the
      same bytes as `PayloadEncoding.ToBytes` produces
      (`MultiKilobyteHexPayload_ValidatesAndEncodesTheSameBytesAsBefore`/
      `MultiKilobyteBase64Payload_ValidatesAndEncodesTheSameBytesAsBefore`).
- [x] 4.5 Add a `lazynats.Core.Tests` case asserting `IsValidHex` on a large payload does not
      allocate proportionally to its length, via `GC.GetAllocatedBytesForCurrentThread()` before/
      after a warmed-up call (`IsValidHex_DoesNotAllocateProportionallyToPayloadLength`).
- [x] 4.6 `dotnet test src/lazynats.Core.Tests/lazynats.Core.Tests.csproj` passes.
