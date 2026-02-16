using System.Diagnostics;
using NDesk.DBus;
using org.freedesktop.DBus;

var socketPath = $"/tmp/ndesk-smoke-bus-{Guid.NewGuid():N}.sock";
var startInfo = new ProcessStartInfo
{
    FileName = "dbus-daemon",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
};

startInfo.ArgumentList.Add("--session");
startInfo.ArgumentList.Add("--fork");
startInfo.ArgumentList.Add("--nopidfile");
startInfo.ArgumentList.Add($"--address=unix:path={socketPath}");
startInfo.ArgumentList.Add("--print-address=1");
startInfo.ArgumentList.Add("--print-pid=1");

using var daemon = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dbus-daemon");

var address = daemon.StandardOutput.ReadLine();
var pidLine = daemon.StandardOutput.ReadLine();
var err = daemon.StandardError.ReadToEnd();

daemon.WaitForExit();
if (daemon.ExitCode != 0 || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(pidLine))
{
    Console.Error.WriteLine("Failed to start dbus-daemon.");
    if (!string.IsNullOrWhiteSpace(err))
    {
        Console.Error.WriteLine(err.Trim());
    }
    return 1;
}

if (!int.TryParse(pidLine, out var daemonPid))
{
    Console.Error.WriteLine($"Invalid dbus-daemon pid output: '{pidLine}'");
    return 1;
}

Console.WriteLine($"Started custom dbus-daemon pid={daemonPid}");
Console.WriteLine($"Address: {address}");

Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", address);

try
{
    var conn = Bus.Session;
    var bus = conn.GetObject<IBus>("org.freedesktop.DBus", new ObjectPath("/org/freedesktop/DBus"));

    var names = bus.ListNames();
    Console.WriteLine($"ListNames returned {names.Length} names.");
    foreach (var name in names)
    {
        Console.WriteLine($" - {name}");
    }

    Console.WriteLine("Smoke test succeeded.");
    return 0;
}
finally
{
    try
    {
        Process.GetProcessById(daemonPid).Kill();
    }
    catch
    {
        // Best-effort daemon cleanup.
    }

    try
    {
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }
    }
    catch
    {
        // Best-effort socket cleanup.
    }
}
