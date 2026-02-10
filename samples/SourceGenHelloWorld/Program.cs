using Avalonia.DBus;

namespace SourceGenHelloWorld;

internal static class Program
{
    private static async Task Main()
    {
        await using var connection = await DBusConnection.ConnectSessionAsync();

        // Manual message construction (sent via the public DBusConnection API)
        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "ListNames");
        var names = (List<string>)reply.Body[0];

        foreach (var name in names)
            Console.WriteLine(name);

        using var subscription = await connection.SubscribeAsync(
            sender: null,
            path: (DBusObjectPath)"/org/freedesktop/Notifications",
            iface: "org.freedesktop.Notifications",
            member: "NotificationClosed",
            handler: async message =>
            {
                var id = (uint)message.Body[0];
                var reason = (uint)message.Body[1];
                Console.WriteLine($"Notification {id} closed with reason {reason}");
            });

        await Task.Delay(Timeout.Infinite);

    }
}
