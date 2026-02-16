using System;
using System.Collections.Generic;
using Xunit;
using NDesk.DBus;

namespace NDesk.DBus.Tests.Unit;

public class MessageTests
{
    [Fact]
    public void ReplyExpected_SetTrue_PreservesOtherFlags()
    {
        var msg = new Message();
        msg.Header.Flags = HeaderFlag.NoReplyExpected | HeaderFlag.NoAutoStart;

        msg.ReplyExpected = true;

        Assert.True((msg.Header.Flags & HeaderFlag.NoAutoStart) != HeaderFlag.None);
        Assert.Equal(HeaderFlag.None, msg.Header.Flags & HeaderFlag.NoReplyExpected);
    }

    [Fact]
    public void Signature_Set_Empty_RemovesFromFields()
    {
        var msg = new Message
        {
            Signature = new Signature("si")
        };
        Assert.True(msg.Header.Fields.ContainsKey(FieldCode.Signature));

        msg.Signature = Signature.Empty;

        Assert.False(msg.Header.Fields.ContainsKey(FieldCode.Signature));
    }
}

public class MethodCallWrapperTests
{
    [Fact]
    public void Constructor_NullInterface_DoesNotAddInterfaceField()
    {
        var mc = new MethodCall(
            new ObjectPath("/org/example"),
            null,
            "Method",
            "org.example.Dest",
            Signature.Empty);

        Assert.False(mc.message.Header.Fields.ContainsKey(FieldCode.Interface));
    }

    [Fact]
    public void MessageConstructor_ExtractsFieldsFromMessage()
    {
        var path = new ObjectPath("/org/example/obj");
        var originalMc = new MethodCall(path, "org.example.Iface", "DoIt", "org.example.Dest", new Signature("si"));
        originalMc.message.Header.Fields[FieldCode.Sender] = ":1.42";

        var reconstructed = new MethodCall(originalMc.message);

        Assert.Equal("/org/example/obj", reconstructed.Path.Value);
        Assert.Equal("org.example.Iface", reconstructed.Interface);
        Assert.Equal("DoIt", reconstructed.Member);
        Assert.Equal("org.example.Dest", reconstructed.Destination);
        Assert.Equal(":1.42", reconstructed.Sender);
        Assert.Equal(new Signature("si"), reconstructed.Signature);
    }
}

public class MethodReturnWrapperTests
{
    [Fact]
    public void Constructor_SetsNoReplyExpectedAndNoAutoStartFlags()
    {
        var mr = new MethodReturn(1);

        var expected = HeaderFlag.NoReplyExpected | HeaderFlag.NoAutoStart;
        Assert.Equal(expected, mr.message.Header.Flags);
    }

    [Fact]
    public void MessageConstructor_ExtractsReplySerial()
    {
        var mr = new MethodReturn(999);

        var reconstructed = new MethodReturn(mr.message);

        Assert.Equal(999u, reconstructed.ReplySerial);
    }
}

public class SignalWrapperTests
{
    [Fact]
    public void MessageConstructor_ExtractsFieldsIncludingSender()
    {
        var original = new Signal(new ObjectPath("/org/test"), "org.test.Iface", "Notify");
        original.message.Header.Fields[FieldCode.Sender] = ":1.100";

        var reconstructed = new Signal(original.message);

        Assert.Equal("/org/test", reconstructed.Path.Value);
        Assert.Equal("org.test.Iface", reconstructed.Interface);
        Assert.Equal("Notify", reconstructed.Member);
        Assert.Equal(":1.100", reconstructed.Sender);
    }
}

public class ErrorWrapperTests
{
    [Fact]
    public void MessageConstructor_ExtractsErrorNameAndReplySerial()
    {
        var original = new Error("org.example.Error.Custom", 456);

        var reconstructed = new Error(original.message);

        Assert.Equal("org.example.Error.Custom", reconstructed.ErrorName);
        Assert.Equal(456u, reconstructed.ReplySerial);
    }
}
