using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using NDesk.DBus;

namespace Avalonia.DBus.WireMarshalling.Tests.NDesk;

public sealed class NDeskMarshallerFactory : IWireMarshallerFactory
{
    public IWireWriter CreateWriter(ByteOrder order)
    {
        return new NDeskWireWriter(ToEndianFlag(order));
    }

    public IWireReader CreateReader(ByteOrder order, byte[] data)
    {
        return new NDeskWireReader(ToEndianFlag(order), data);
    }

    public IMessageBuilder CreateMessageBuilder() => new NDeskMessageBuilder();

    public IMessageMarshaller CreateMarshaller() => new NDeskMessageMarshaller();

    public ISignatureBuilder CreateSignatureBuilder() => new NDeskSignatureBuilder();

    private static EndianFlag ToEndianFlag(ByteOrder order) =>
        order == ByteOrder.LittleEndian ? EndianFlag.Little : EndianFlag.Big;
}
