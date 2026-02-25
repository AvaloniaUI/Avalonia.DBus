namespace Avalonia.DBus.WireMarshalling.Tests.Abstractions;

public interface IMessageBuilder
{
    void SetMessageType(DBusMessageType type);
    void SetSerial(uint serial);
    void SetReplySerial(uint replySerial);
    void SetPath(string? path);
    void SetInterface(string? iface);
    void SetMember(string? member);
    void SetDestination(string? dest);
    void SetSender(string? sender);
    void SetNoReply(bool noReply);
    void AppendArgs(Action<IWireWriter> writeAction);
    string? GetPath();
    string? GetInterface();
    string? GetMember();
    string? GetDestination();
    string? GetSender();
    uint GetSerial();
    uint GetReplySerial();
    bool GetNoReply();
    string GetSignature();
    void SetSignature(string signature);
    IWireReader GetBodyReader();
    byte[] GetHeaderBytes();
    byte[] GetBodyBytes();
}
