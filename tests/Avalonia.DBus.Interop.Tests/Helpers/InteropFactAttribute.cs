namespace Avalonia.DBus.Interop.Tests.Helpers;

/// <summary>
/// Integration test attribute with a 5-second timeout.
/// Use only for Async tests.
/// </summary>
public sealed class InteropFactAttribute : IntegrationFactAttribute
{
    private const int DefaultTimeoutMs = 5000;

    public InteropFactAttribute()
    {
        if (Timeout <= 0)
            Timeout = DefaultTimeoutMs;
    }
}
