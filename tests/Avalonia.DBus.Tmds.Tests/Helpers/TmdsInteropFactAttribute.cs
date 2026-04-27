using Avalonia.DBus.Testing;

namespace Avalonia.DBus.Tmds.Tests.Helpers;

/// <summary>
/// Integration test attribute with a 5-second timeout for Tmds interop tests.
/// </summary>
public sealed class TmdsInteropFactAttribute : IntegrationFactAttribute
{
    private const int DefaultTimeoutMs = 5000;

    public TmdsInteropFactAttribute()
    {
        if (Timeout <= 0)
            Timeout = DefaultTimeoutMs;
    }
}
