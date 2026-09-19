using System.Text;
using lazynats.Core.Payloads;

namespace lazynats.Core.Tests.Payloads;

// Covers allocation-free-payload-content-probe's tasks.md section 2: every scenario in
// openspec/specs/payload-content-probe/spec.md, the full C0/C1 control-byte sweep plus UTF-8
// boundary non-controls, the JsonDocument.TryParseValue trailing-content pitfall, and the
// no-proportional-allocation guarantee itself.
public class PayloadContentProbeTests
{
    [Fact]
    public void ValidJson_ClassifiesAsJson()
    {
        Assert.Equal(PayloadContentKind.Json, PayloadContentProbe.Classify("""{"a":1}"""u8.ToArray()));
    }

    [Fact]
    public void NonJsonText_ClassifiesAsUtf8Text()
    {
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify("hello world"u8.ToArray()));
    }

    [Fact]
    public void MalformedUtf8_ClassifiesAsBinary()
    {
        Assert.Equal(PayloadContentKind.Binary, PayloadContentProbe.Classify([0xFF, 0xFE]));
    }

    [Fact]
    public void EmptyPayload_ClassifiesAsUtf8Text()
    {
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify([]));
    }

    [Fact]
    public void EmojiAndNonAsciiText_NeverClassifiesAsBinary()
    {
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("héllo 🎉")));
        Assert.Equal(PayloadContentKind.Json, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("""{"emoji":"🎉","name":"héllo"}""")));
    }

    [Fact]
    public void MalformedButMostlyAscii_ClassifiesAsBinary()
    {
        var payload = Encoding.UTF8.GetBytes("hello").Concat([(byte)0xC3]).ToArray(); // truncated lead byte
        Assert.Equal(PayloadContentKind.Binary, PayloadContentProbe.Classify(payload));
    }

    [Fact]
    public void EmbeddedNonAllowedControlCharacter_ClassifiesAsBinary()
    {
        Assert.Equal(PayloadContentKind.Binary, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("hello\u0001world")));
    }

    [Fact]
    public void OnlyTabLineFeedCarriageReturnControlCharacters_DoesNotClassifyAsBinary()
    {
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("hello\tworld\r\n")));
    }

    [Theory]
    [InlineData(0x80)]
    [InlineData(0x81)]
    [InlineData(0x9E)]
    [InlineData(0x9F)]
    public void C1ControlCharacter_StandaloneOrEmbedded_ClassifiesAsBinary(int codepoint)
    {
        var c = (char)codepoint;
        Assert.Equal(PayloadContentKind.Binary, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes(c.ToString())));
        Assert.Equal(PayloadContentKind.Binary, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes($"hello{c}world")));
    }

    [Fact]
    public void FullC1Range_AllClassifyAsBinary()
    {
        for (var codepoint = 0x80; codepoint <= 0x9F; codepoint++)
        {
            var payload = Encoding.UTF8.GetBytes(((char)codepoint).ToString());
            Assert.Equal(PayloadContentKind.Binary, PayloadContentProbe.Classify(payload));
        }
    }

    [Theory]
    [InlineData(0x00A0)] // NBSP
    [InlineData(0x0104)] // Ą
    public void C2LedNonControlCharacter_DoesNotClassifyAsBinary(int codepoint)
    {
        var c = (char)codepoint;
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes($"hello{c}world")));
    }

    [Fact]
    public void Emoji_DoesNotClassifyAsBinary()
    {
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("hello🎉world")));
    }

    [Fact]
    public void JsonValueFollowedByTrailingGarbage_ClassifiesAsUtf8TextNotJson()
    {
        Assert.Equal(PayloadContentKind.Utf8Text, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("""{"a":1} garbage""")));
    }

    [Fact]
    public void JsonValueFollowedByTrailingWhitespace_StillClassifiesAsJson()
    {
        Assert.Equal(PayloadContentKind.Json, PayloadContentProbe.Classify(Encoding.UTF8.GetBytes("{\"a\":1}   \r\n")));
    }

    [Fact]
    public void Classify_OnLargeBinaryPayload_DoesNotAllocateProportionallyToPayloadLength()
    {
        var payload = new byte[128_000];
        new Random(1).NextBytes(payload);
        payload[0] = 0xFF; // guarantee malformed UTF-8 regardless of the random fill

        PayloadContentProbe.Classify(payload); // warm up (JIT, SearchValues init, ...) before measuring

        var before = GC.GetAllocatedBytesForCurrentThread();
        var kind = PayloadContentProbe.Classify(payload);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(PayloadContentKind.Binary, kind);
        // A proportional allocation would be on the order of the payload size (~128,000 bytes);
        // the span-based path should allocate nothing proportional to it.
        Assert.True(allocated < 4_096, $"expected a small, length-independent allocation, got {allocated} bytes");
    }
}
