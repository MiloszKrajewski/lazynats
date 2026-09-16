using System.Buffers;

namespace lazynats.Payloads;

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
        foreach (var range in text.SplitAny(Whitespace)) {
            var fragment = text[range];
            if (fragment.IsEmpty) continue;
            if (fragment.Length % unitSize != 0) { bytes = []; return false; }
            maxBytes += fragment.Length / unitSize * maxBytesPerUnit;
        }

        var buffer = new byte[maxBytes];
        var offset = 0;
        foreach (var range in text.SplitAny(Whitespace)) {
            var fragment = text[range];
            if (fragment.IsEmpty) continue;
            if (!decodeFragment(fragment, buffer.AsSpan(offset), out var written)) { bytes = []; return false; }
            offset += written;
        }

        bytes = offset == buffer.Length ? buffer : buffer[..offset];
        return true;
    }
}
