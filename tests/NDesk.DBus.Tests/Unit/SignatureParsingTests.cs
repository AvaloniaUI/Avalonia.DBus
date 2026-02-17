using System;
using System.Collections.Generic;
using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class SignatureParsingTests
{
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
    [InlineData("f", typeof(float))]
    [InlineData("s", typeof(string))]
    [InlineData("v", typeof(object))]
    public void ToType_BasicTypes_ReturnsCorrectClrType(string sigStr, Type expected)
    {
        var sig = new Signature(sigStr);

        var result = sig.ToType();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToType_ObjectPath_ReturnsObjectPathType()
    {
        var sig = new Signature("o");

        var result = sig.ToType();

        Assert.Equal(typeof(ObjectPath), result);
    }

    [Fact]
    public void ToType_DictStringVariant_ReturnsIDictionaryStringObject()
    {
        var sig = new Signature("a{sv}");

        var result = sig.ToType();

        Assert.Equal(typeof(IDictionary<string, object>), result);
    }

    [Fact]
    public void ToType_NestedArrayOfStringArrays_ReturnsStringArrayArray()
    {
        var sig = new Signature("aas");

        var result = sig.ToType();

        Assert.Equal(typeof(string[][]), result);
    }

    [Fact]
    public void ToTypes_MixedWithDict_CorrectlyParsesAll()
    {
        // "ia{sv}s" = int, dict<string,object>, string
        var sig = new Signature("ia{sv}s");

        var result = sig.ToTypes();

        Assert.Equal(3, result.Length);
        Assert.Equal(typeof(int), result[0]);
        Assert.Equal(typeof(IDictionary<string, object>), result[1]);
        Assert.Equal(typeof(string), result[2]);
    }

    [Fact]
    public void ToType_MultiTypeSignature_ThrowsException()
    {
        var sig = new Signature("si");

        var ex = Assert.Throws<Exception>(() => sig.ToType());

        Assert.Contains("not a single complete type", ex.Message);
    }

    [Fact]
    public void ToType_StructBegin_ThrowsNotSupportedException()
    {
        var sig = new Signature("(ii)");

        Assert.Throws<NotSupportedException>(() => sig.ToType());
    }
}
