using System;
using Xunit;
using NDesk.DBus;

namespace NDesk.DBus.Tests.Unit;

public class SignatureTests
{
    [Fact]
    public void Construction_FromString_MatchesByteArray()
    {
        var fromString = new Signature("siu");
        var fromBytes = new Signature(new byte[] { (byte)'s', (byte)'i', (byte)'u' });

        Assert.True(fromString == fromBytes);
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

        Assert.Equal(DType.String, sig[0]);
        Assert.Equal(DType.Int32, sig[1]);
        Assert.Equal(DType.UInt32, sig[2]);
    }
}
