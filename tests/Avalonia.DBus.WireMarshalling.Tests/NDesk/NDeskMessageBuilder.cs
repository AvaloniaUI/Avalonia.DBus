using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using NDesk.DBus;

namespace Avalonia.DBus.WireMarshalling.Tests.NDesk;

internal sealed class NDeskMessageBuilder : IMessageBuilder
{
    internal readonly Message Message;

    public NDeskMessageBuilder()
    {
        Message = new Message();
        // Default: MethodCall with NoReplyExpected cleared (so NoReply defaults to false)
        Message.Header.Flags = HeaderFlag.None;
    }

    internal NDeskMessageBuilder(Message message)
    {
        Message = message;
    }

    public void SetMessageType(DBusMessageType type)
    {
        Message.Header.MessageType = (MessageType)(byte)type;
    }

    public void SetSerial(uint serial)
    {
        Message.Header.Serial = serial;
    }

    public void SetReplySerial(uint replySerial)
    {
        Message.Header.Fields[FieldCode.ReplySerial] = replySerial;
    }

    public void SetPath(string? path)
    {
        if (path == null)
            Message.Header.Fields.Remove(FieldCode.Path);
        else
            Message.Header.Fields[FieldCode.Path] = new ObjectPath(path);
    }

    public void SetInterface(string? iface)
    {
        if (iface == null)
            Message.Header.Fields.Remove(FieldCode.Interface);
        else
            Message.Header.Fields[FieldCode.Interface] = iface;
    }

    public void SetMember(string? member)
    {
        if (member == null)
            Message.Header.Fields.Remove(FieldCode.Member);
        else
            Message.Header.Fields[FieldCode.Member] = member;
    }

    public void SetDestination(string? dest)
    {
        if (dest == null)
            Message.Header.Fields.Remove(FieldCode.Destination);
        else
            Message.Header.Fields[FieldCode.Destination] = dest;
    }

    public void SetSender(string? sender)
    {
        if (sender == null)
            Message.Header.Fields.Remove(FieldCode.Sender);
        else
            Message.Header.Fields[FieldCode.Sender] = sender;
    }

    public void SetNoReply(bool noReply)
    {
        if (noReply)
            Message.Header.Flags |= HeaderFlag.NoReplyExpected;
        else
            Message.Header.Flags &= ~HeaderFlag.NoReplyExpected;
    }

    public void AppendArgs(Action<IWireWriter> writeAction)
    {
        using var writer = new NDeskWireWriter(Message.Header.Endianness);
        writeAction(writer);
        var bodyBytes = writer.ToArray();

        if (Message.Body != null)
        {
            // Append to existing body
            var combined = new byte[Message.Body.Length + bodyBytes.Length];
            Buffer.BlockCopy(Message.Body, 0, combined, 0, Message.Body.Length);
            Buffer.BlockCopy(bodyBytes, 0, combined, Message.Body.Length, bodyBytes.Length);
            Message.Body = combined;
        }
        else
        {
            Message.Body = bodyBytes;
        }
    }

    public string? GetPath()
    {
        if (Message.Header.Fields.TryGetValue(FieldCode.Path, out var val))
            return val?.ToString();
        return null;
    }

    public string? GetInterface()
    {
        if (Message.Header.Fields.TryGetValue(FieldCode.Interface, out var val))
            return val as string;
        return null;
    }

    public string? GetMember()
    {
        if (Message.Header.Fields.TryGetValue(FieldCode.Member, out var val))
            return val as string;
        return null;
    }

    public string? GetDestination()
    {
        if (Message.Header.Fields.TryGetValue(FieldCode.Destination, out var val))
            return val as string;
        return null;
    }

    public string? GetSender()
    {
        if (Message.Header.Fields.TryGetValue(FieldCode.Sender, out var val))
            return val as string;
        return null;
    }

    public uint GetSerial() => Message.Header.Serial;

    public uint GetReplySerial()
    {
        if (Message.Header.Fields.TryGetValue(FieldCode.ReplySerial, out var val))
            return (uint)val;
        return 0;
    }

    public bool GetNoReply() => (Message.Header.Flags & HeaderFlag.NoReplyExpected) != 0;

    public string GetSignature() => Message.Signature.Value;

    public void SetSignature(string signature)
    {
        Message.Signature = new Signature(signature);
    }

    public IWireReader GetBodyReader()
    {
        return new NDeskWireReader(Message.Header.Endianness, Message.Body ?? []);
    }

    public byte[] GetHeaderBytes() => Message.GetHeaderData();

    public byte[] GetBodyBytes() => Message.Body ?? [];
}
