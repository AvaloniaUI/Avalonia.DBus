using Avalonia.DBus;

namespace SourceGenHelloWorld;

internal static class Program
{
    private static async Task Main()
    {
        await using var wire = await DBusWireConnection.ConnectSessionAsync();

        // Manual message construction
        var message = DBusMessage.CreateMethodCall(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "ListNames");

        var reply = await wire.SendWithReplyAsync(message);
        var names = (List<string>)reply.Body[0];

        foreach (var name in names)
            Console.WriteLine(name);

        await using var connection = await DBusConnection.ConnectSessionAsync();

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
