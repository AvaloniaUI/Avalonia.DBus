using System;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class SignatureParserTests
{
    [Theory]
    [InlineData("y", "y")]
    [InlineData("b", "b")]
    [InlineData("n", "n")]
    [InlineData("q", "q")]
    [InlineData("i", "i")]
    [InlineData("u", "u")]
    [InlineData("x", "x")]
    [InlineData("t", "t")]
    [InlineData("d", "d")]
    [InlineData("s", "s")]
    [InlineData("o", "o")]
    [InlineData("g", "g")]
    [InlineData("h", "h")]
    [InlineData("v", "v")]
    public void ReadSingleType_BasicTypes(string signature, string expected)
    {
        var index = 0;
        var result = DBusSignatureParser.ReadSingleType(signature, ref index);

        Assert.Equal(expected, result);
        Assert.Equal(signature.Length, index);
    }

    [Theory]
    [InlineData("ai", "ai")]
    [InlineData("aai", "aai")]
    [InlineData("as", "as")]
    public void ReadSingleType_Arrays(string signature, string expected)
    {
        var index = 0;
        var result = DBusSignatureParser.ReadSingleType(signature, ref index);

        Assert.Equal(expected, result);
        Assert.Equal(expected.Length, index);
    }

    [Fact]
    public void ReadSingleType_DictEntry()
    {
        var signature = "a{sv}";
        var index = 0;
        var result = DBusSignatureParser.ReadSingleType(signature, ref index);

        Assert.Equal("a{sv}", result);
        Assert.Equal(5, index);
    }

    [Theory]
    [InlineData("(ii)", "(ii)")]
    [InlineData("(suu)", "(suu)")]
    [InlineData("(i(ss))", "(i(ss))")]
    public void ReadSingleType_Structs(string signature, string expected)
    {
        var index = 0;
        var result = DBusSignatureParser.ReadSingleType(signature, ref index);

        Assert.Equal(expected, result);
        Assert.Equal(expected.Length, index);
    }

    [Fact]
    public void ReadSingleType_FirstTypeFromMultiple()
    {
        var signature = "isu";
        var index = 0;
        var result = DBusSignatureParser.ReadSingleType(signature, ref index);

        Assert.Equal("i", result);
        Assert.Equal(1, index);
    }

    [Theory]
    [InlineData("(ii)", new[] { "i", "i" })]
    [InlineData("(suu)", new[] { "s", "u", "u" })]
    [InlineData("(ias)", new[] { "i", "as" })]
    public void ParseStructSignatures_ReturnsParts(string signature, string[] expected)
    {
        var parts = DBusSignatureParser.ParseStructSignatures(signature);

        Assert.Equal(expected, parts);
    }

    [Fact]
    public void ParseStructSignatures_NestedStruct()
    {
        var parts = DBusSignatureParser.ParseStructSignatures("(i(ss))");

        Assert.Equal(["i", "(ss)"], parts);
    }

    [Theory]
    [InlineData("{sv}", "s", "v")]
    [InlineData("{si}", "s", "i")]
    [InlineData("{ua{sv}}", "u", "a{sv}")]
    public void ParseDictEntrySignatures_ReturnsKeyAndValue(string signature, string expectedKey, string expectedValue)
    {
        var (key, value) = DBusSignatureParser.ParseDictEntrySignatures(signature);

        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("i")]
    public void ParseStructSignatures_InvalidInput_Throws(string signature)
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ParseStructSignatures(signature));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sv")]
    public void ParseDictEntrySignatures_InvalidInput_Throws(string signature)
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ParseDictEntrySignatures(signature));
    }

    [Fact]
    public void ReadSingleType_NullSignature_Throws()
    {
        var index = 0;
        Assert.Throws<ArgumentNullException>(() => DBusSignatureParser.ReadSingleType(null!, ref index));
    }

    [Fact]
    public void ReadSingleType_IndexOutOfRange_Throws()
    {
        var index = 5;
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ReadSingleType("i", ref index));
    }

    [Fact]
    public void ParseStructSignatures_UnclosedStruct_Throws()
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ParseStructSignatures("(ii"));
    }

    [Fact]
    public void ParseStructSignatures_TrailingData_Throws()
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ParseStructSignatures("(ii)x"));
    }

    [Fact]
    public void ParseDictEntrySignatures_UnclosedEntry_Throws()
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ParseDictEntrySignatures("{sv"));
    }

    [Fact]
    public void ParseDictEntrySignatures_TrailingData_Throws()
    {
        Assert.Throws<ArgumentException>(() => DBusSignatureParser.ParseDictEntrySignatures("{sv}x"));
    }
}
