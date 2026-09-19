using System.Buffers;

namespace lazynats.Core.Payloads;

// Shared whitespace-tolerant Hex/Base64 decoder behind PayloadValidation.IsValidHex/IsValidBase64
// and PayloadEncoding.ToBytes - see design.md's "Boundary validation and decoding via spans" and
// "One shared decode routine" decisions. Whitespace is only accepted between complete encoding
// units (a 2-hex-digit byte pair, a 4-character Base64 quantum); whitespace splitting a unit makes
// the text invalid. Scans/decodes directly from the input span into one pre-sized output buffer -
// no intermediate whitespace-stripped string, so this stays cheap enough to run on every keystroke.
internal static class PayloadBinaryText
{
    // Fixed set rather than char.IsWhiteSpace's full Unicode category - SearchValues.Create needs
    // concrete values, not a predicate. Strictly more lenient than the pre-existing (whitespace-free
    // only) behavior, never less, so this is a bounded scope of leniency, not a regression.
    private static readonly SearchValues<char> Whitespace = SearchValues.Create(" \t\r\n");

    public static bool TryDecodeHex(ReadOnlySpan<char> text, out byte[] bytes) =>
        TryDecode(text, unitSize: 2, maxBytesPerUnit: 1, DecodeHexFragment, out bytes);

    public static bool TryDecodeBase64(ReadOnlySpan<char> text, out byte[] bytes) =>
        TryDecode(text, unitSize: 4, maxBytesPerUnit: 3, DecodeBase64Fragment, out bytes);

    // Common multiple of both unit sizes (2, 4), so it never splits a Hex byte pair or Base64
    // quantum internally - see design.md's "Validate-only path decodes in fixed-size chunks"
    // decision. Sized well below a thread's default 1MB stack.
    private const int ChunkChars = 1024;
    
    // TODO: (IsValidHex/IsValidBase64)
    //  to check if something is valid we don't need to decode it,
    //  just check if fragment has right set of characters
    //  base64 padding is a little bit more complicated as we need to know it it is last chunk,
    //  and if it is trim trailing `=`

    // Validate-only counterparts to TryDecodeHex/TryDecodeBase64: same fragment/boundary rules,
    // but decode each fragment in ChunkChars-sized pieces into one reused stackalloc buffer
    // instead of one heap buffer sized to the whole payload, discarding each chunk's decoded
    // bytes and only checking success. PayloadEncoding.ToBytes still needs the owned array
    // TryDecodeHex/TryDecodeBase64 return, so those are unchanged.
    public static bool IsValidHex(ReadOnlySpan<char> text) =>
        IsValid(text, unitSize: 2, maxBytesPerUnit: 1, DecodeHexFragment, checkPadding: false);

    public static bool IsValidBase64(ReadOnlySpan<char> text) =>
        IsValid(text, unitSize: 4, maxBytesPerUnit: 3, DecodeBase64Fragment, checkPadding: true);

    private delegate bool FragmentDecoder(ReadOnlySpan<char> fragment, Span<byte> destination, out int written);

    private static bool DecodeHexFragment(ReadOnlySpan<char> fragment, Span<byte> destination, out int written)
    {
        var status = Convert.FromHexString(fragment, destination, out var charsConsumed, out written);
        return status == OperationStatus.Done && charsConsumed == fragment.Length;
    }

    private static bool DecodeBase64Fragment(ReadOnlySpan<char> fragment, Span<byte> destination, out int written) =>
        Convert.TryFromBase64Chars(fragment, destination, out written);

    // Two passes over the same fragments: the first validates boundaries and sums each fragment's
    // worst-case decoded size (exact for Hex, an upper bound for Base64 since only a final padded
    // quantum decodes to fewer bytes) so the output buffer can be allocated once; the second
    // decodes each fragment directly into its slice of that buffer.
    private static bool TryDecode(
        ReadOnlySpan<char> text, int unitSize, int maxBytesPerUnit, FragmentDecoder decodeFragment, out byte[] bytes)
    {
        var maxBytes = 0;
        foreach (var range in text.SplitAny(Whitespace))
        {
            var fragment = text[range];
            if (fragment.IsEmpty) continue;

            if (fragment.Length % unitSize != 0)
            {
                bytes = [];
                return false;
            }

            maxBytes += fragment.Length / unitSize * maxBytesPerUnit;
        }

        var buffer = new byte[maxBytes];
        var offset = 0;
        foreach (var range in text.SplitAny(Whitespace))
        {
            var fragment = text[range];
            if (fragment.IsEmpty) continue;

            if (!decodeFragment(fragment, buffer.AsSpan(offset), out var written))
            {
                bytes = [];
                return false;
            }

            offset += written;
        }

        bytes = offset == buffer.Length ? buffer : buffer[..offset];
        return true;
    }

    // Same fragment split/boundary check as TryDecode, but chunks each fragment into ChunkChars
    // pieces (last piece may be shorter; both are guaranteed multiples of unitSize since
    // ChunkChars % unitSize == 0 and the fragment's own length already is) and decodes each piece
    // into one stackalloc buffer allocated once, before the fragment loop, and reused across every
    // chunk of every fragment.
    //
    // checkPadding (Base64 only - Hex has no padding concept) guards against a whole-string-invalid
    // input that chunking alone would wrongly accept: Convert.TryFromBase64Chars rejects a '='
    // that isn't part of a trailing pad (e.g. "QQ==QQ==" fails as one decode), but two independent
    // 4-char chunks ("QQ==", "QQ==") would each succeed on its own. seenPadding tracks, across the
    // whole scan (all chunks, all fragments, in order), whether a chunk containing '=' has been
    // decoded; once it has, any later non-empty chunk - in the same fragment or a later one - makes
    // the payload invalid regardless of what its own decode call reports.
    private static bool IsValid(
        ReadOnlySpan<char> text, int unitSize, int maxBytesPerUnit, FragmentDecoder decodeFragment, bool checkPadding)
    {
        Span<byte> scratch = stackalloc byte[ChunkChars / unitSize * maxBytesPerUnit];
        var seenPadding = false;

        foreach (var range in text.SplitAny(Whitespace))
        {
            var fragment = text[range];
            if (fragment.IsEmpty) continue;

            if (fragment.Length % unitSize != 0) return false;

            for (var offset = 0; offset < fragment.Length; offset += ChunkChars)
            {
                if (checkPadding && seenPadding) return false;

                var chunk = fragment.Slice(offset, Math.Min(ChunkChars, fragment.Length - offset));
                if (!decodeFragment(chunk, scratch, out _)) return false;

                if (checkPadding && chunk.Contains('=')) seenPadding = true;
            }
        }

        return true;
    }
}
