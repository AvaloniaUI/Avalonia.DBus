using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class SignatureTests
{
    [Fact]
    public void Construction_FromString_MatchesByteArray()
    {
        var fromString = new Signature("siu");
        var fromBytes = new Signature("siu"u8.ToArray());

        Assert.Equal(fromString, fromBytes);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("siu")]
    [InlineData("a{sv}")]
    public void Value_ReturnsOriginalString(string input)
    {
        var sig = new Signature(input);

        Assert.Equal(input, sig.Value);
    }

    [Fact]
    public void Indexer_ReturnsDTypeAtPosition()
    {
        var sig = new Signature("siu");

        Assert.Equal(new[] { DType.String, DType.Int32, DType.UInt32 },
            new[] { sig[0], sig[1], sig[2] });
    }
}
