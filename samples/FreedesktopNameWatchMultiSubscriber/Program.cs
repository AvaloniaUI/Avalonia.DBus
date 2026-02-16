using System.Diagnostics;
using Avalonia.DBus;

namespace FreedesktopNameWatchMultiSubscriber;

internal static class Program
{
    private static async Task Main()
    {
        await using var connection = await DBusConnection.ConnectSessionAsync();

        var proxyA = connection.CreateFreedesktopDBusProxy();
        var proxyB = connection.CreateFreedesktopDBusProxy();

        var watchedName = $"org.avalonia.DBus.MultiSubscriberDemo.p{Environment.ProcessId}.g{Guid.NewGuid():N}";
        Console.WriteLine($"Watching name: {watchedName}");

        var hitA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hitB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subA = await proxyA.WatchNameOwnerChangedAsync((name, oldOwner, newOwner) =>
        {
            if (!string.Equals(name, watchedName, StringComparison.Ordinal))
                return;

            Console.WriteLine($"[A] NameOwnerChanged old='{oldOwner}' new='{newOwner}'");
            hitA.TrySetResult(true);
        });

        using var subB = await proxyB.WatchNameOwnerChangedAsync((name, oldOwner, newOwner) =>
        {
            if (!string.Equals(name, watchedName, StringComparison.Ordinal))
                return;

            Console.WriteLine($"[B] NameOwnerChanged old='{oldOwner}' new='{newOwner}'");
            hitB.TrySetResult(true);
        });

        var requestResult = await connection.RequestNameAsync(watchedName);
        Console.WriteLine($"RequestName result: {requestResult}");

        var completed = await WaitForBothAsync(hitA.Task, hitB.Task, TimeSpan.FromSeconds(5));
        if (!completed)
            throw new InvalidOperationException("Expected both subscribers to receive NameOwnerChanged, but timed out.");

        Console.WriteLine("Both subscribers received NameOwnerChanged.");

        try
        {
            await connection.ReleaseNameAsync(watchedName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ReleaseName warning: {ex.Message}");
        }
    }

    private static async Task<bool> WaitForBothAsync(Task first, Task second, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (first.IsCompletedSuccessfully && second.IsCompletedSuccessfully)
                return true;

            await Task.Delay(25);
        }

        return first.IsCompletedSuccessfully && second.IsCompletedSuccessfully;
    }
}
