using System;
using Xunit.Abstractions;

namespace Avalonia.DBus.Interop.Tests.Helpers;

internal sealed class TestOutputDiagnostics(ITestOutputHelper output) : IDBusDiagnostics
{
    public void Log(DBusLogLevel level, string message)
        => output.WriteLine($"[Avalonia] [{level}] {message}");

    public void OnUnobservedException(Exception exception)
        => output.WriteLine($"[Avalonia] [Exception] {exception}");
}
