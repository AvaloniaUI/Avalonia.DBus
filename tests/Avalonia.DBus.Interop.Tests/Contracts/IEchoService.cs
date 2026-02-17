using NDesk.DBus;

namespace Avalonia.DBus.Interop.Tests.Contracts;

[Interface("org.avalonia.dbus.interop.Echo")]
public interface IEchoService
{
    string Echo(string message);
    int Add(int a, int b);
    string Concat(string a, string b);
    long Negate(long value);
}
