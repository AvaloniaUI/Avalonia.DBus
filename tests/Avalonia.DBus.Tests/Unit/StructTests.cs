using System.Linq;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class StructTests
{
    [Fact]
    public void Constructor_FromParams_StoresFields()
    {
        var s = new DBusStruct("hello", 42, true);

        Assert.Equal(["hello", 42, true], s);
    }

    [Fact]
    public void Constructor_FromEnumerable_StoresFields()
    {
        object[] fields = ["a", 1, 2.0];
        var s = new DBusStruct(fields.AsEnumerable());

        Assert.Equal(["a", 1, 2.0], s);
    }

    [Fact]
    public void EmptyStruct_HasZeroCount()
    {
        var s = new DBusStruct();

        Assert.Empty(s);
    }

    [Fact]
    public void Indexer_ReturnsCorrectField()
    {
        var s = new DBusStruct(10, 20, 30);

        Assert.Equal([10, 20, 30], s);
    }

    [Fact]
    public void Enumeration_YieldsAllFields()
    {
        var s = new DBusStruct("a", "b", "c");

        var items = s.ToList();

        Assert.Equal(new object[] { "a", "b", "c" }, items);
    }

    [Fact]
    public void IReadOnlyList_Count_Matches()
    {
        System.Collections.Generic.IReadOnlyList<object> s = new DBusStruct(1, 2);

        Assert.Equal([1, 2], s);
    }
}
