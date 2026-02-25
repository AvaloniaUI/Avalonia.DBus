namespace Avalonia.DBus.WireMarshalling.Tests.Abstractions;

public interface IWireMarshallerFactory
{
    IWireWriter CreateWriter(ByteOrder order);
    IWireReader CreateReader(ByteOrder order, byte[] data);
    IMessageBuilder CreateMessageBuilder();
    IMessageMarshaller CreateMarshaller();
    ISignatureBuilder CreateSignatureBuilder();
}
