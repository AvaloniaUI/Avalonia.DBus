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

}
