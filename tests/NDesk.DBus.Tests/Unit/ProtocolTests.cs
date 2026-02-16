using System;
using Xunit;
using NDesk.DBus;

namespace NDesk.DBus.Tests.Unit;

public class ProtocolTests
{
    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 3)]
    [InlineData(2, 4, 2)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 4, 0)]
    [InlineData(5, 8, 3)]
    [InlineData(7, 8, 1)]
    [InlineData(8, 8, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(13, 8, 3)]
    [InlineData(16, 8, 0)]
    public void PadNeeded_ReturnsCorrectPadding(int pos, int alignment, int expected)
    {
        var result = Protocol.PadNeeded(pos, alignment);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(1, 4, 4)]
    [InlineData(4, 4, 4)]
    [InlineData(5, 8, 8)]
    [InlineData(9, 4, 12)]
    [InlineData(0, 1, 0)]
    [InlineData(7, 1, 7)]
    [InlineData(3, 2, 4)]
    [InlineData(6, 2, 6)]
    [InlineData(15, 8, 16)]
    public void Padded_ReturnsCorrectPaddedPosition(int pos, int alignment, int expected)
    {
        var result = Protocol.Padded(pos, alignment);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData((byte)'y', 1)]
    [InlineData((byte)'b', 4)]
    [InlineData((byte)'n', 2)]
    [InlineData((byte)'q', 2)]
    [InlineData((byte)'i', 4)]
    [InlineData((byte)'u', 4)]
    [InlineData((byte)'x', 8)]
    [InlineData((byte)'t', 8)]
    [InlineData((byte)'d', 8)]
    [InlineData((byte)'f', 4)]
    [InlineData((byte)'s', 4)]
    [InlineData((byte)'o', 4)]
    [InlineData((byte)'g', 1)]
    [InlineData((byte)'a', 4)]
    [InlineData((byte)'r', 8)]
    [InlineData((byte)'v', 1)]
    [InlineData((byte)'e', 8)]
    public void GetAlignment_ReturnsCorrectValue(byte dtypeByte, int expectedAlignment)
    {
        var dtype = (DType)dtypeByte;
        var result = Protocol.GetAlignment(dtype);

        Assert.Equal(expectedAlignment, result);
    }
}
