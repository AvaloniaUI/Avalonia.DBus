using System;
using System.Collections.Generic;
using Xunit;
using NDesk.DBus;

namespace NDesk.DBus.Tests.Unit;

public class SignatureInferenceTests
{
    [Fact]
    public void GetSig_IntArray_ReturnsAi()
    {
        var sig = Signature.GetSig(typeof(int[]));

        Assert.Equal("ai", sig.Value);
    }

    [Fact]
    public void GetSig_IDictionaryStringObject_ReturnsASV()
    {
        var sig = Signature.GetSig(typeof(IDictionary<string, object>));

        Assert.Equal("a{sv}", sig.Value);
    }

    [Fact]
    public void GetSig_TypeArray_CombinesSignatures()
    {
        var sig = Signature.GetSig(new Type[] { typeof(string), typeof(int), typeof(uint) });

        Assert.Equal("siu", sig.Value);
    }

    [Fact]
    public void GetSig_NullType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Signature.GetSig((Type)null));
    }

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(int))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(double))]
    [InlineData(typeof(float))]
    [InlineData(typeof(string))]
    [InlineData(typeof(object))]
    public void Roundtrip_GetSigThenToType_ReturnsSameType(Type type)
    {
        var sig = Signature.GetSig(type);
        var result = sig.ToType();

        Assert.Equal(type, result);
    }

    [Fact]
    public void Roundtrip_DictStringObject_PreservesAsIDictionary()
    {
        var sig = Signature.GetSig(typeof(Dictionary<string, object>));
        var result = sig.ToType();

        Assert.Equal(typeof(IDictionary<string, object>), result);
    }
}
