using System;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class MessageTests
{
    [Fact]
    public void CreateMethodCall_SetsCorrectFields()
    {
        var msg = DBusMessage.CreateMethodCall(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "Hello");

        Assert.Equal(DBusMessageType.MethodCall, msg.Type);
        Assert.Equal("org.freedesktop.DBus", msg.Destination);
        Assert.Equal("/org/freedesktop/DBus", msg.Path!.Value.Value);
        Assert.Equal("org.freedesktop.DBus", msg.Interface);
        Assert.Equal("Hello", msg.Member);
        Assert.Empty(msg.Body);
    }

    [Fact]
    public void CreateMethodCall_WithBody_SetsBodyAndSignature()
    {
        var msg = DBusMessage.CreateMethodCall(
            "org.test",
            (DBusObjectPath)"/test",
            "org.test.Iface",
            "Method",
            "hello", 42);

        Assert.Equal(["hello", 42], msg.Body);
        Assert.Equal("si", msg.Signature.Value);
    }

    [Fact]
    public void CreateSignal_SetsCorrectFields()
    {
        var msg = DBusMessage.CreateSignal(
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameOwnerChanged",
            "test", "old", "new");

        Assert.Equal(DBusMessageType.Signal, msg.Type);
        Assert.Equal("/org/freedesktop/DBus", msg.Path!.Value.Value);
        Assert.Equal("org.freedesktop.DBus", msg.Interface);
        Assert.Equal("NameOwnerChanged", msg.Member);
        Assert.Equal(3, msg.Body.Count);
        Assert.Null(msg.Destination);
    }

    [Fact]
    public void CreateReply_SetsReplySerial()
    {
        var request = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Destination = "org.test",
            Interface = "org.test.Iface",
            Member = "Method"
        };
        request.Serial = 42;
        request.Sender = ":1.100";

        var reply = request.CreateReply("result");

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
        Assert.Equal(42u, reply.ReplySerial);
        Assert.Equal(":1.100", reply.Destination);
        Assert.Single(reply.Body);
        Assert.Equal("result", reply.Body[0]);
    }

    [Fact]
    public void CreateError_SetsErrorName()
    {
        var request = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
        };
        request.Serial = 7;
        request.Sender = ":1.50";

        var error = request.CreateError("org.freedesktop.DBus.Error.Failed", "something went wrong");

        Assert.Equal(DBusMessageType.Error, error.Type);
        Assert.Equal(7u, error.ReplySerial);
        Assert.Equal(":1.50", error.Destination);
        Assert.Equal("org.freedesktop.DBus.Error.Failed", error.ErrorName);
        Assert.Single(error.Body);
        Assert.Equal("something went wrong", error.Body[0]);
    }

    [Fact]
    public void CreateError_WithoutMessage_HasEmptyBody()
    {
        var request = new DBusMessage { Type = DBusMessageType.MethodCall };
        request.Serial = 1;

        var error = request.CreateError("org.test.Error");

        Assert.Equal("org.test.Error", error.ErrorName);
        Assert.Empty(error.Body);
    }

    [Fact]
    public void CreateError_EmptyName_Throws()
    {
        var msg = new DBusMessage { Type = DBusMessageType.MethodCall };

        Assert.Throws<ArgumentException>(() => msg.CreateError(""));
    }

    [Fact]
    public void Body_Setter_InfersSignature()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Body = ["hello", 42, true]
        };

        Assert.Equal("sib", msg.Signature.Value);
    }

    [Fact]
    public void Body_EmptyBody_EmptySignature()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Body = []
        };

        Assert.Equal(string.Empty, msg.Signature.Value);
    }

    [Fact]
    public void IsMethodCall_MatchesCorrectly()
    {
        var msg = DBusMessage.CreateMethodCall("dest", (DBusObjectPath)"/", "org.test", "Ping");

        Assert.True(msg.IsMethodCall("org.test", "Ping"));
        Assert.False(msg.IsMethodCall("org.test", "Other"));
        Assert.False(msg.IsMethodCall("org.other", "Ping"));
    }

    [Fact]
    public void IsSignal_MatchesCorrectly()
    {
        var msg = DBusMessage.CreateSignal((DBusObjectPath)"/", "org.test", "Changed");

        Assert.True(msg.IsSignal("org.test", "Changed"));
        Assert.False(msg.IsSignal("org.test", "Other"));
        Assert.False(msg.IsSignal("org.other", "Changed"));
    }

    [Fact]
    public void IsError_MatchesCorrectly()
    {
        var request = new DBusMessage { Type = DBusMessageType.MethodCall };
        request.Serial = 1;
        var error = request.CreateError("org.test.Error.NotFound");

        Assert.True(error.IsError("org.test.Error.NotFound"));
        Assert.False(error.IsError("org.test.Error.Other"));
    }

    [Fact]
    public void Flags_NoReplyExpected()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Flags = DBusMessageFlags.NoReplyExpected
        };

        Assert.Equal(DBusMessageFlags.NoReplyExpected, msg.Flags);
    }

    [Fact]
    public void Flags_AllowInteractiveAuthorization()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Flags = DBusMessageFlags.AllowInteractiveAuthorization
        };

        Assert.Equal(DBusMessageFlags.AllowInteractiveAuthorization, msg.Flags);
    }

    [Fact]
    public void Flags_Combined()
    {
        var flags = DBusMessageFlags.NoReplyExpected | DBusMessageFlags.NoAutoStart;
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Flags = flags
        };

        Assert.True(msg.Flags.HasFlag(DBusMessageFlags.NoReplyExpected));
        Assert.True(msg.Flags.HasFlag(DBusMessageFlags.NoAutoStart));
    }

    [Fact]
    public void CreateMethodCall_EmptyDestination_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DBusMessage.CreateMethodCall("", (DBusObjectPath)"/", "org.test", "Method"));
    }

    [Fact]
    public void CreateMethodCall_EmptyInterface_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DBusMessage.CreateMethodCall("dest", (DBusObjectPath)"/", "", "Method"));
    }

    [Fact]
    public void CreateMethodCall_EmptyMember_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DBusMessage.CreateMethodCall("dest", (DBusObjectPath)"/", "org.test", ""));
    }

    [Fact]
    public void CreateSignal_EmptyInterface_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DBusMessage.CreateSignal((DBusObjectPath)"/", "", "Member"));
    }

    [Fact]
    public void CreateSignal_EmptyMember_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DBusMessage.CreateSignal((DBusObjectPath)"/", "org.test", ""));
    }
}
