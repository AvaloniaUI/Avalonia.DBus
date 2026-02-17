namespace Avalonia.DBus.Interop.Tests.Contracts;

public class CounterService : ICounterService
{
    private int _value;

    public event CounterChangedHandler? ValueChanged;

    public int GetValue() => _value;

    public void Increment()
    {
        var old = _value;
        _value++;
        ValueChanged?.Invoke(old, _value);
    }

    public void Decrement()
    {
        var old = _value;
        _value--;
        ValueChanged?.Invoke(old, _value);
    }

    public void Reset()
    {
        var old = _value;
        _value = 0;
        ValueChanged?.Invoke(old, _value);
    }
}
