using System.Text;
using lazynats.Core.Payloads;

namespace lazynats.Core.Tests.Payloads;

// Covers hex-base64-allocation-free-validation's tasks.md section 3: parity between the new
// validate-only IsValidHex/IsValidBase64 and the existing TryDecodeHex/TryDecodeBase64 (which
// PayloadEncoding.ToBytes still uses), the Base64 padding-suffix edge case, a multi-chunk payload,
// and the no-proportional-allocation guarantee itself.
public class PayloadBinaryTextTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("48656c6c6f", true)]
    [InlineData("48 65 6c 6c 6f", true)]
    [InlineData("48656C6C6F\r\n", true)]
    [InlineData("4", false)] // odd length
    [InlineData("4 8", false)] // whitespace splits a byte pair
    [InlineData("zz", false)] // not hex digits
    public void IsValidHex_MatchesTryDecodeHex(string text, bool expectedValid)
    {
        Assert.Equal(expectedValid, PayloadBinaryText.TryDecodeHex(text, out _));
        Assert.Equal(expectedValid, PayloadBinaryText.IsValidHex(text));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("SGVsbG8=", true)]
    [InlineData("SGVs bG8=", true)]
    [InlineData("SGVsbG8", false)] // not a multiple of 4
    [InlineData("!!!!", false)] // not base64 characters
    [InlineData("QQ==QQ==", false)] // padding followed by more data, single fragment
    public void IsValidBase64_MatchesTryDecodeBase64(string text, bool expectedValid)
    {
        Assert.Equal(expectedValid, PayloadBinaryText.TryDecodeBase64(text, out _));
        Assert.Equal(expectedValid, PayloadBinaryText.IsValidBase64(text));
    }

    [Fact]
    public void IsValidBase64_RejectsPaddingFollowedByMoreDataInTheSameFragment()
    {
        // A single fragment (no whitespace) where '=' padding is followed by more data - the
        // shape called out in tasks.md 3.3. Convert.TryFromBase64Chars rejects this fed whole, so
        // TryDecodeBase64 already rejects it too; IsValidBase64 must agree.
        const string text = "QQ==QQ==";
        Assert.False(PayloadBinaryText.TryDecodeBase64(text, out _));
        Assert.False(PayloadBinaryText.IsValidBase64(text));
    }

    [Fact]
    public void IsValidBase64_RejectsPaddingHiddenAtAChunkBoundary()
    {
        // Regression test for design.md's "Base64 padding must be a suffix of the whole text"
        // decision: builds a single fragment (no whitespace) longer than ChunkChars (1024) whose
        // padded quantum lands exactly at the end of the first 1024-char chunk, with more valid-
        // looking data in the next chunk. Each chunk decodes fine standalone - only tracking
        // "padding already seen" across chunks catches this; per-chunk decoding alone would not.
        var head = Convert.ToBase64String(RandomBytes(765, seed: 1)); // 1020 chars, unpadded
        var tail = Convert.ToBase64String(RandomBytes(150, seed: 2)); // 200 chars, unpadded
        var text = head + "QQ==" + tail; // 1224 chars, one fragment: padding ends exactly at char 1024

        Assert.Equal(1024, head.Length + "QQ==".Length);
        Assert.False(PayloadBinaryText.TryDecodeBase64(text, out _));
        Assert.False(PayloadBinaryText.IsValidBase64(text));
    }

    private static byte[] RandomBytes(int count, int seed)
    {
        var bytes = new byte[count];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    // Breaks the encoded text into many short lines so the payload has several whitespace-
    // delimited fragments, each itself spanning several 1024-char chunks (PayloadBinaryText's
    // ChunkChars) - exercising both the fragment loop and the chunk loop within a fragment.
    private static string InsertNewlines(string text)
    {
        var builder = new StringBuilder();
        for (var offset = 0; offset < text.Length; offset += 200)
            builder.Append(text.AsSpan(offset, Math.Min(200, text.Length - offset))).Append('\n');
        return builder.ToString();
    }

    [Fact]
    public void MultiKilobyteHexPayload_ValidatesAndEncodesTheSameBytesAsBefore()
    {
        var expected = new byte[5_000];
        new Random(12345).NextBytes(expected);
        var text = InsertNewlines(Convert.ToHexString(expected));

        Assert.True(PayloadValidation.IsValid(PayloadType.Hex, text));
        Assert.True(PayloadBinaryText.TryDecodeHex(text, out var decoded));
        Assert.Equal(expected, decoded);
        Assert.Equal(expected, PayloadEncoding.ToBytes(PayloadType.Hex, text));
    }

    [Fact]
    public void MultiKilobyteBase64Payload_ValidatesAndEncodesTheSameBytesAsBefore()
    {
        var expected = new byte[5_000];
        new Random(67890).NextBytes(expected);
        var text = InsertNewlines(Convert.ToBase64String(expected));

        Assert.True(PayloadValidation.IsValid(PayloadType.Base64, text));
        Assert.True(PayloadBinaryText.TryDecodeBase64(text, out var decoded));
        Assert.Equal(expected, decoded);
        Assert.Equal(expected, PayloadEncoding.ToBytes(PayloadType.Base64, text));
    }

    [Fact]
    public void IsValidHex_DoesNotAllocateProportionallyToPayloadLength()
    {
        var bytes = new byte[64];
        new Random(1).NextBytes(bytes);
        var unit = Convert.ToHexString(bytes);
        var large = string.Concat(Enumerable.Repeat(unit, 2_000)); // 256,000 chars, ~128,000 decoded bytes

        PayloadBinaryText.IsValidHex(large); // warm up (JIT, SearchValues init, ...) before measuring

        var before = GC.GetAllocatedBytesForCurrentThread();
        var isValid = PayloadBinaryText.IsValidHex(large);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(isValid);
        // A proportional allocation would be on the order of the decoded size (~128,000 bytes);
        // the chunked, stackalloc-backed path should allocate nothing proportional to it.
        Assert.True(allocated < 4_096, $"expected a small, length-independent allocation, got {allocated} bytes");
    }
}
