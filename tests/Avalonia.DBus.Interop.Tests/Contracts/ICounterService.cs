using NDesk.DBus;

namespace Avalonia.DBus.Interop.Tests.Contracts;

public delegate void CounterChangedHandler(int oldValue, int newValue);

[Interface("org.avalonia.dbus.interop.Counter")]
public interface ICounterService
{
    int GetValue();
    void Increment();
    void Decrement();
    void Reset();
    event CounterChangedHandler ValueChanged;
}
