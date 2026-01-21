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

    public void ReplyIntrospectXml(ReadOnlySpan<ReadOnlyMemory<byte>> interfaceXmls)
    {
        if (NoReplyExpected)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append("<node>");
        if (!interfaceXmls.IsEmpty)
        {
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
        }

        foreach (var xml in interfaceXmls)
        {
            builder.Append(Encoding.UTF8.GetString(xml.ToArray()));
        }
        builder.Append("</node>");

        var writer = CreateReplyWriter("s");
        writer.WriteString(builder.ToString());
        Reply(writer.CreateMessage());
        writer.Dispose();
    }
}
