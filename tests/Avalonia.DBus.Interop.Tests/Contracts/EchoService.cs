namespace Avalonia.DBus.Interop.Tests.Contracts;

public class EchoService : IEchoService
{
    public string Echo(string message) => message;

    public int Add(int a, int b) => a + b;

    public string Concat(string a, string b) => a + b;

    public long Negate(long value) => -value;
}
