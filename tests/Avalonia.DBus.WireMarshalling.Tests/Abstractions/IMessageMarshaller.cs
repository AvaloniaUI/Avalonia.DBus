namespace Avalonia.DBus.WireMarshalling.Tests.Abstractions;

public interface IMessageMarshaller
{
    byte[] Marshal(IMessageBuilder message);
    IMessageBuilder Demarshal(byte[] data);
    int BytesNeeded(byte[] partial, int length);
}
