using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace NDesk.DBus.Tests.Helpers;

public sealed class DbusDaemonFixture : IAsyncLifetime
{
    private Process _process;

    public string Address { get; private set; }

    public bool IsAvailable => Address is not null;

    public async Task InitializeAsync()
    {
        var daemonPath = FindDbusDaemon();
        if (daemonPath is null)
            return;

        var configPath = Path.Combine(AppContext.BaseDirectory, "Helpers", "test-session.conf");
        if (!File.Exists(configPath))
            return;

        var psi = new ProcessStartInfo
        {
            FileName = daemonPath,
            Arguments = $"--nofork --print-address --config-file=\"{configPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = psi };
        _process.Start();

        var addressLine = await _process.StandardOutput.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(addressLine))
        {
            Kill();
            return;
        }

        Address = addressLine.Trim();
    }

    public Task DisposeAsync()
    {
        Kill();
        return Task.CompletedTask;
    }

    private void Kill()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // best-effort
        }

        _process.Dispose();
        _process = null;
    }

    internal static string FindDbusDaemon()
    {
        string[] candidates =
        [
            "/usr/bin/dbus-daemon",
            "/opt/homebrew/bin/dbus-daemon",
            "/usr/local/bin/dbus-daemon",
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
