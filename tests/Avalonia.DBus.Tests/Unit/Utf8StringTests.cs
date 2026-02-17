using System;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class Utf8StringTests
{
    [Fact]
    public unsafe void Encodes_Ascii_Correctly()
    {
        using var utf8 = new Utf8String("Hello");

        Assert.NotEqual(IntPtr.Zero, (IntPtr)utf8.Pointer);

        var span = new ReadOnlySpan<byte>(utf8.Pointer, 6); // 5 chars + null terminator
        Assert.Equal("Hello\0"u8.ToArray(), span.ToArray());
    }

    [Fact]
    public unsafe void Encodes_Unicode_Correctly()
    {
        using var utf8 = new Utf8String("\u00E9"); // é

        Assert.NotEqual(IntPtr.Zero, (IntPtr)utf8.Pointer);

        // UTF-8 encoding of é (U+00E9) is 0xC3 0xA9
        var span = new ReadOnlySpan<byte>(utf8.Pointer, 3); // 2 bytes + null
        Assert.Equal("\u00E9\0"u8.ToArray(), span.ToArray());
    }

    [Fact]
    public unsafe void EmptyString_Handled()
    {
        using var utf8 = new Utf8String("");

        Assert.NotEqual(IntPtr.Zero, (IntPtr)utf8.Pointer);

        // Should just be a null terminator
        Assert.Equal(0, *utf8.Pointer);
    }

    [Fact]
    public unsafe void NullString_PointerIsNull()
    {
        using var utf8 = new Utf8String(null!);

        Assert.Equal(IntPtr.Zero, (IntPtr)utf8.Pointer);
    }

    [Fact]
    public void Dispose_FreesHandle()
    {
        var utf8 = new Utf8String("test");
        utf8.Dispose();

        // Second dispose should not throw
        utf8.Dispose();
    }

    [Fact]
    public unsafe void MultiByteUnicode_EncodedCorrectly()
    {
        // Snowman U+2603 -> 0xE2 0x98 0x83
        using var utf8 = new Utf8String("\u2603");

        var span = new ReadOnlySpan<byte>(utf8.Pointer, 4);
        Assert.Equal("\u2603\0"u8.ToArray(), span.ToArray());
    }
}
