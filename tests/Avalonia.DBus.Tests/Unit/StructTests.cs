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

}
