using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class SignatureTests
{
    [Fact]
    public void NullValue_CoalescesToEmptyString()
    {
        var sig = new DBusSignature(null!);

        Assert.Equal(string.Empty, sig.Value);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new DBusSignature("iss");
        var b = new DBusSignature("iss");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = new DBusSignature("i");
        var b = new DBusSignature("s");

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var sig = new DBusSignature("a{sv}");
        string value = sig;

        Assert.Equal("a{sv}", value);
    }

    [Fact]
    public void ExplicitConversion_FromString_CreatesSignature()
    {
        var sig = (DBusSignature)"a{sv}";

        Assert.Equal("a{sv}", sig.Value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var sig = new DBusSignature("(iiu)");

        Assert.Equal("(iiu)", sig.ToString());
    }

    [Theory]
    [InlineData("y")]
    [InlineData("b")]
    [InlineData("n")]
    [InlineData("q")]
    [InlineData("i")]
    [InlineData("u")]
    [InlineData("x")]
    [InlineData("t")]
    [InlineData("d")]
    [InlineData("s")]
    [InlineData("o")]
    [InlineData("g")]
    [InlineData("h")]
    [InlineData("v")]
    public void SingleTypeCodes_PreservedAsValue(string code)
    {
        var sig = new DBusSignature(code);

        Assert.Equal(code, sig.Value);
    }

    [Fact]
    public void Equals_WithNonSignature_ReturnsFalse()
    {
        var sig = new DBusSignature("i");

        Assert.All(new object?[] { "not a sig", 42, null }, x => Assert.False(sig.Equals(x)));
    }
}
