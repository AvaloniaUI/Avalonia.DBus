using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class CollectionHelpersTests
{
    [Fact]
    public void TryGetListElementType_IntArray_ReturnsInt()
    {
        var result = DBusCollectionHelpers.TryGetListElementType(typeof(int[]), out var elementType);

        Assert.True(result);
        Assert.Equal(typeof(int), elementType);
    }

    [Fact]
    public void TryGetListElementType_ListOfString_ReturnsString()
    {
        var result = DBusCollectionHelpers.TryGetListElementType(typeof(List<string>), out var elementType);

        Assert.True(result);
        Assert.Equal(typeof(string), elementType);
    }

    [Fact]
    public void TryGetListElementType_NonList_ReturnsFalse()
    {
        var result = DBusCollectionHelpers.TryGetListElementType(typeof(string), out _);

        Assert.False(result);
    }

    [Fact]
    public void TryGetListElementType_IReadOnlyList_ReturnsElementType()
    {
        var result = DBusCollectionHelpers.TryGetListElementType(typeof(IReadOnlyList<double>), out var elementType);

        Assert.True(result);
        Assert.Equal(typeof(double), elementType);
    }

    [Fact]
    public void TryGetDictionaryTypes_DictionaryStringInt_ReturnsTypes()
    {
        var result = DBusCollectionHelpers.TryGetDictionaryTypes(
            typeof(Dictionary<string, int>), out var keyType, out var valueType);

        Assert.True(result);
        Assert.Equal(typeof(string), keyType);
        Assert.Equal(typeof(int), valueType);
    }

    [Fact]
    public void TryGetDictionaryTypes_NonDict_ReturnsFalse()
    {
        var result = DBusCollectionHelpers.TryGetDictionaryTypes(typeof(List<int>), out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryGetDictionaryTypes_IReadOnlyDictionary_ReturnsTypes()
    {
        var result = DBusCollectionHelpers.TryGetDictionaryTypes(
            typeof(IReadOnlyDictionary<string, DBusVariant>), out var keyType, out var valueType);

        Assert.True(result);
        Assert.Equal(typeof(string), keyType);
        Assert.Equal(typeof(DBusVariant), valueType);
    }

    [Fact]
    public void EnumerateListItems_ReturnsAllItems()
    {
        var list = new List<int> { 10, 20, 30 };
        var items = DBusCollectionHelpers.EnumerateListItems(list).ToList();

        Assert.Equal(new object[] { 10, 20, 30 }, items);
    }

    [Fact]
    public void EnumerateDictionaryEntries_ReturnsAllEntries()
    {
        var dict = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 }
        };

        var entries = DBusCollectionHelpers.EnumerateDictionaryEntries(dict).ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => (string)e.Key! == "one" && (int)e.Value! == 1);
        Assert.Contains(entries, e => (string)e.Key! == "two" && (int)e.Value! == 2);
    }

    [Fact]
    public void EnumerateListItems_NonEnumerable_ReturnsEmpty()
    {
        var items = DBusCollectionHelpers.EnumerateListItems(42).ToList();

        Assert.Empty(items);
    }
}
