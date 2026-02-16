using System;
using NDesk.DBus;
using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class ObjectPathTests
{
    [Fact]
    public void Root_HasSlashValue()
    {
        Assert.Equal("/", ObjectPath.Root.Value);
    }

    [Fact]
    public void Constructor_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ObjectPath(null));
    }

    [Fact]
    public void Decomposed_ThreeLevelPath_ReturnsComponents()
    {
        var path = new ObjectPath("/a/b/c");

        var parts = path.Decomposed;

        Assert.Equal(new[] { "a", "b", "c" }, parts);
    }

    [Fact]
    public void Parent_OfTwoLevelPath_ReturnsParent()
    {
        var path = new ObjectPath("/com/example");

        var parent = path.Parent;

        Assert.Equal("/com", parent.Value);
    }

    [Fact]
    public void Parent_ChainedToRoot_EventuallyReturnsNull()
    {
        var path = new ObjectPath("/a/b/c");

        var current = path;
        var depth = 0;
        while (current != null)
        {
            current = current.Parent;
            depth++;
        }

        // /a/b/c -> /a/b -> /a -> / -> null = 4 steps
        Assert.Equal(4, depth);
    }
}
