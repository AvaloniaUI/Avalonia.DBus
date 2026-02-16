using System;
using System.Collections.Generic;
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
    public void Constructor_InfersSignature_ForObjectPath()
    {
        var path = new DBusObjectPath("/org/test");
        var variant = new DBusVariant(path);

        Assert.Equal("o", variant.Signature.Value);
    }

    [Fact]
    public void Constructor_InfersSignature_ForSignature()
    {
        var sig = new DBusSignature("ai");
        var variant = new DBusVariant(sig);

        Assert.Equal("g", variant.Signature.Value);
    }

    [Fact]
    public void Constructor_InfersSignature_ForUnixFd()
    {
        var fd = new DBusUnixFd(5);
        var variant = new DBusVariant(fd);

        Assert.Equal("h", variant.Signature.Value);
    }

    [Fact]
    public void Constructor_InfersSignature_ForNestedVariant()
    {
        var inner = new DBusVariant(42);
        var outer = new DBusVariant(inner);

        Assert.Equal("v", outer.Signature.Value);
    }

    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DBusVariant(null!));
    }

    [Fact]
    public void Value_ReturnsOriginal()
    {
        var original = "test string";
        var variant = new DBusVariant(original);

        Assert.Same(original, variant.Value);
    }

    [Fact]
    public void Constructor_InfersSignature_ForList()
    {
        List<int> list = [1, 2, 3];
        var variant = new DBusVariant(list);

        Assert.Equal("ai", variant.Signature.Value);
    }

    [Fact]
    public void Constructor_InfersSignature_ForDictionary()
    {
        var dict = new Dictionary<string, int> { { "a", 1 } };
        var variant = new DBusVariant(dict);

        Assert.Equal("a{si}", variant.Signature.Value);
    }
}
