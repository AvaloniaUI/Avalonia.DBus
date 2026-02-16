using System;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class VariantTests
{
    [Theory]
    [InlineData((byte)42, "y")]
    [InlineData(true, "b")]
    [InlineData((short)-1, "n")]
    [InlineData((ushort)65535, "q")]
    [InlineData(42, "i")]
    [InlineData(42u, "u")]
    [InlineData(42L, "x")]
    [InlineData(42UL, "t")]
    [InlineData(3.14, "d")]
    [InlineData("hello", "s")]
    public void Constructor_InfersSignature_ForPrimitives(object value, string expectedSignature)
    {
        var variant = new DBusVariant(value);

        Assert.Equal(expectedSignature, variant.Signature.Value);
        Assert.Same(value, variant.Value);
    }

    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DBusVariant(null!));
    }
}
