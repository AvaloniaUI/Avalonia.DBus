using System;
using System.Text;
using Avalonia.DBus.AutoGen;

namespace Avalonia.DBus.Wire;

public sealed unsafe class MethodContext
{
    private readonly DBusMessage* _request;

    internal MethodContext(Connection connection, DBusMessage* request)
    {
        Connection = connection;
        _request = request;
        Request = new Message(request);
        NoReplyExpected = dbus.dbus_message_get_no_reply(request) != 0;
    }

    public Connection Connection { get; }

    public Message Request { get; }

    public bool NoReplyExpected { get; }

    public MessageWriter CreateReplyWriter(string? signature)
    {
        _ = signature;
        return MessageWriter.CreateReply(_request);
    }

    public void Reply(MessageBuffer message)
    {
        if (NoReplyExpected)
        {
            message.Dispose();
            return;
        }
        Connection.TrySendMessage(message);
    }

    public void ReplyError(string errorName, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorName))
        {
            throw new ArgumentException("Error name must be provided.", nameof(errorName));
        }

        if (NoReplyExpected)
        {
            return;
        }

        using var nameUtf8 = new Utf8String(errorName);
        using var textUtf8 = new Utf8String(errorMessage ?? string.Empty);
        var error = dbus.dbus_message_new_error(_request, nameUtf8.Pointer, textUtf8.Pointer);
        if (error == null)
        {
            throw new InvalidOperationException("Failed to create error reply.");
        }

        using var buffer = new MessageBuffer(error);
        Connection.TrySendMessage(buffer);
    }

    public void ReplyIntrospectXml(ReadOnlySpan<ReadOnlyMemory<byte>> interfaceXmls)
        => ReplyIntrospectXml(interfaceXmls, ReadOnlySpan<string>.Empty);

    public void ReplyIntrospectXml(ReadOnlySpan<ReadOnlyMemory<byte>> interfaceXmls, ReadOnlySpan<string> childNodes)
    {
        if (NoReplyExpected)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append("<node>");
        builder.Append("<interface name=\"org.freedesktop.DBus.Introspectable\">\n");
        builder.Append("  <method name=\"Introspect\">\n");
        builder.Append("    <arg name=\"data\" type=\"s\" direction=\"out\"/>\n");
        builder.Append("  </method>\n");
        builder.Append("</interface>\n");
        builder.Append("<interface name=\"org.freedesktop.DBus.Peer\">\n");
        builder.Append("  <method name=\"Ping\"/>\n");
        builder.Append("  <method name=\"GetMachineId\">\n");
        builder.Append("    <arg name=\"machine_uuid\" type=\"s\" direction=\"out\"/>\n");
        builder.Append("  </method>\n");
        builder.Append("</interface>\n");

        foreach (var xml in interfaceXmls)
        {
            builder.Append(Encoding.UTF8.GetString(xml.ToArray()));
        }

        foreach (var child in childNodes)
        {
            builder.Append("<node name=\"");
            builder.Append(child);
            builder.Append("\"/>\n");
        }

        builder.Append("</node>");

        var writer = CreateReplyWriter("s");
        writer.WriteString(builder.ToString());
        Reply(writer.CreateMessage());
        writer.Dispose();
    }
}
