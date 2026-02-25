using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using NDesk.DBus;

namespace Avalonia.DBus.WireMarshalling.Tests.NDesk;

public sealed class NDeskMessageMarshaller : IMessageMarshaller
{
    public byte[] Marshal(IMessageBuilder message)
    {
        var msg = ((NDeskMessageBuilder)message).Message;
        return MessageWire.Marshal(msg);
    }

    public IMessageBuilder Demarshal(byte[] data)
    {
        var msg = MessageWire.Demarshal(data);
        return new NDeskMessageBuilder(msg);
    }

    public int BytesNeeded(byte[] partial, int length)
    {
        return MessageWire.BytesNeeded(partial, length);
    }
}
