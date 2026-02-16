using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class ObjectPathTests
{
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new DBusObjectPath("/org/freedesktop/DBus");
        var b = new DBusObjectPath("/org/freedesktop/DBus");

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
        var a = new DBusObjectPath("/org/freedesktop/DBus");
        var b = new DBusObjectPath("/org/freedesktop/Other");

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_WithNonObjectPath_ReturnsFalse()
    {
        var path = new DBusObjectPath("/org/freedesktop/DBus");

        Assert.All(new object?[] { "not a path", 42, null }, x => Assert.False(path.Equals(x)));
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var path = new DBusObjectPath("/org/freedesktop/DBus");
        string value = path;

        Assert.Equal("/org/freedesktop/DBus", value);
    }

    [Fact]
    public void ExplicitConversion_FromString_CreatesPath()
    {
        var path = (DBusObjectPath)"/org/freedesktop/DBus";

        Assert.Equal("/org/freedesktop/DBus", path.Value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var path = new DBusObjectPath("/org/freedesktop/DBus");

        Assert.Equal("/org/freedesktop/DBus", path.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/org")]
    [InlineData("/org/freedesktop/DBus")]
    [InlineData("/a/b/c/d/e/f")]
    public void Value_PreservesInput(string input)
    {
        var path = new DBusObjectPath(input);

        Assert.Equal(input, path.Value);
    }
}
