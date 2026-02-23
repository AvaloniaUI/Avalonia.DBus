using System;
using System.Collections.Generic;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class SignatureInferenceTests
{
    [Theory]
    [InlineData((byte)0, "y")]
    [InlineData(true, "b")]
    [InlineData((short)0, "n")]
    [InlineData((ushort)0, "q")]
    [InlineData(0, "i")]
    [InlineData(0u, "u")]
    [InlineData(0L, "x")]
    [InlineData(0UL, "t")]
    [InlineData(0.0, "d")]
    [InlineData("hello", "s")]
    public void InferSignatureFromValue_Primitives(object value, string expected)
    {
        var result = DBusSignatureInference.InferSignatureFromValue(value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void InferSignatureFromValue_ObjectPath()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(new DBusObjectPath("/test"));

        Assert.Equal("o", result);
    }

    [Fact]
    public void InferSignatureFromValue_Signature()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(new DBusSignature("i"));

        Assert.Equal("g", result);
    }

    [Fact]
    public void InferSignatureFromValue_UnixFd()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(new DBusUnixFd(0));

        Assert.Equal("h", result);
    }

    [Fact]
    public void InferSignatureFromValue_Variant()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(new DBusVariant(42));

        Assert.Equal("v", result);
    }

    [Fact]
    public void InferSignatureFromValue_List()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(new List<int> { 1, 2, 3 });

        Assert.Equal("ai", result);
    }

    [Fact]
    public void InferSignatureFromValue_Dictionary()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(
            new Dictionary<string, DBusVariant> { { "key", new DBusVariant(1) } });

        Assert.Equal("a{sv}", result);
    }

    [Fact]
    public void InferSignatureFromValue_Struct()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(
            new DBusStruct("hello", 42));

        Assert.Equal("(si)", result);
    }

    [Fact]
    public void InferSignatureFromValue_StructConvertible()
    {
        var result = DBusSignatureInference.InferSignatureFromValue(new TestStructConvertible());

        Assert.Equal("(si)", result);
    }

    [Fact]
    public void InferSignatureFromValue_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DBusSignatureInference.InferSignatureFromValue(null!));
    }

    [Theory]
    [InlineData(typeof(byte), "y")]
    [InlineData(typeof(bool), "b")]
    [InlineData(typeof(short), "n")]
    [InlineData(typeof(ushort), "q")]
    [InlineData(typeof(int), "i")]
    [InlineData(typeof(uint), "u")]
    [InlineData(typeof(long), "x")]
    [InlineData(typeof(ulong), "t")]
    [InlineData(typeof(double), "d")]
    [InlineData(typeof(string), "s")]
    [InlineData(typeof(DBusObjectPath), "o")]
    [InlineData(typeof(DBusSignature), "g")]
    [InlineData(typeof(DBusUnixFd), "h")]
    [InlineData(typeof(DBusVariant), "v")]
    public void InferSignatureFromType_Primitives(Type type, string expected)
    {
        var result = DBusSignatureInference.InferSignatureFromType(type);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void InferSignatureFromType_ListOfInt()
    {
        var result = DBusSignatureInference.InferSignatureFromType(typeof(List<int>));

        Assert.Equal("ai", result);
    }

    [Fact]
    public void InferSignatureFromType_DictionaryStringVariant()
    {
        var result = DBusSignatureInference.InferSignatureFromType(typeof(Dictionary<string, DBusVariant>));

        Assert.Equal("a{sv}", result);
    }

    [Fact]
    public void InferSignatureFromType_IntArray()
    {
        var result = DBusSignatureInference.InferSignatureFromType(typeof(int[]));

        Assert.Equal("ai", result);
    }

    [Fact]
    public void InferSignatureFromType_DBusStruct_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => DBusSignatureInference.InferSignatureFromType(typeof(DBusStruct)));
    }

    [Fact]
    public void InferSignatureFromType_StructConvertible_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => DBusSignatureInference.InferSignatureFromType(typeof(TestStructConvertible)));
    }

    [Theory]
    [InlineData("y", typeof(byte))]
    [InlineData("b", typeof(bool))]
    [InlineData("n", typeof(short))]
    [InlineData("q", typeof(ushort))]
    [InlineData("i", typeof(int))]
    [InlineData("u", typeof(uint))]
    [InlineData("x", typeof(long))]
    [InlineData("t", typeof(ulong))]
    [InlineData("d", typeof(double))]
    [InlineData("s", typeof(string))]
    [InlineData("o", typeof(DBusObjectPath))]
    [InlineData("g", typeof(DBusSignature))]
    [InlineData("h", typeof(DBusUnixFd))]
    [InlineData("v", typeof(DBusVariant))]
    public void GetTypeForSignature_BasicTypes(string signature, Type expected)
    {
        var result = DBusSignatureInference.GetTypeForSignature(signature);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTypeForSignature_Array()
    {
        var result = DBusSignatureInference.GetTypeForSignature("ai");

        Assert.Equal(typeof(List<int>), result);
    }

    [Fact]
    public void GetTypeForSignature_DictEntry()
    {
        var result = DBusSignatureInference.GetTypeForSignature("a{sv}");

        Assert.Equal(typeof(Dictionary<string, DBusVariant>), result);
    }

    [Fact]
    public void GetTypeForSignature_Struct()
    {
        var result = DBusSignatureInference.GetTypeForSignature("(ii)");

        Assert.Equal(typeof(DBusStruct), result);
    }

    [Fact]
    public void GetTypeForSignature_EmptySignature_Throws()
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureInference.GetTypeForSignature(""));
    }

    [Fact]
    public void InferBodySignature_MultipleArgs()
    {
        var body = new object[] { "hello", 42, true };
        var result = DBusSignatureInference.InferBodySignature(body);

        Assert.Equal("sib", result);
    }

    [Fact]
    public void InferBodySignature_EmptyBody()
    {
        var result = DBusSignatureInference.InferBodySignature([]);

        Assert.Equal(string.Empty, result);
    }

    private sealed class TestStructConvertible : IDBusStructConvertible
    {
        public DBusStruct ToDbusStruct() => new("hello", 42);
    }
}
