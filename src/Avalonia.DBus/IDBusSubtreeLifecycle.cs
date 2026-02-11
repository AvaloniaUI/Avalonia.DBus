namespace Avalonia.DBus;

public interface IDBusSubtreeLifecycle
{
    void OnConnectedToTree(DBusConnection connection, string fullPath);

    void OnDisconnectedFromTree(DBusConnection connection, string fullPath);
}