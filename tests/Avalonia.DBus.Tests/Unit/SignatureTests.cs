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
    public void Equals_WithNonSignature_ReturnsFalse()
    {
        var sig = new DBusSignature("i");

        Assert.All(new object?[] { "not a sig", 42, null }, x => Assert.False(sig.Equals(x)));
    }
}
