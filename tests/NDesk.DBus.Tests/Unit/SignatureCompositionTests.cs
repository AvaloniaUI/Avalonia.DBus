using System;
using NDesk.DBus;
using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class SignatureCompositionTests
{
    [Fact]
    public void OperatorPlus_ConcatenatesTwoSignatures()
    {
        var s = new Signature("s");
        var i = new Signature("i");

        var result = s + i;

        Assert.Equal("si", result.Value);
    }

    [Fact]
    public void MakeArraySignature_OnInt32_ProducesArrayOfInt32()
    {
        var sig = new Signature("i");

        var array = sig.MakeArraySignature();

        Assert.Equal("ai", array.Value);
    }

    [Fact]
    public void MakeStruct_MultipleElements_ProducesParenthesizedConcatenation()
    {
        var result = Signature.MakeStruct(new Signature("s"), new Signature("i"));

        Assert.Equal("(si)", result.Value);
    }

    [Fact]
    public void MakeDictEntry_StringKeyVariantValue_ProducesCurlyBracedEntry()
    {
        var result = Signature.MakeDictEntry(new Signature("s"), new Signature("v"));

        Assert.Equal("{sv}", result.Value);
    }

    [Fact]
    public void MakeDict_StringKeyVariantValue_ProducesArrayOfDictEntry()
    {
        var result = Signature.MakeDict(new Signature("s"), new Signature("v"));

        Assert.Equal("a{sv}", result.Value);
    }

    [Fact]
    public void IsArray_TrueForSimpleArray()
    {
        var sig = new Signature("ai");

        Assert.True(sig.IsArray);
    }

    [Fact]
    public void IsDict_FalseForStandardDictSignature_DueToIndexCheck()
    {
        // IsDict checks this[2] == DictEntryBegin, but for "a{sv}":
        //   index 0='a', index 1='{', index 2='s'
        // So IsDict returns false -- this is a known quirk of the implementation.
        var sig = new Signature("a{sv}");

        Assert.False(sig.IsDict);
    }

    [Fact]
    public void GetElementSignature_OnSimpleIntArray_ReturnsElementType()
    {
        var sig = new Signature("ai");

        var element = sig.GetElementSignature();

        Assert.Equal("i", element.Value);
    }
}
