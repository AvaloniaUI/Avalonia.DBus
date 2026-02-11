using Avalonia.DBus;
using Avalonia.DBus.SourceGen;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class CacheHandler(AtspiServer server) : IOrgA11yAtspiCache
{
    public uint Version => CacheVersion;

    public ValueTask<List<AtSpiAccessibleCacheItem>> GetItemsAsync()
    {
        AccessibleNode[] snapshot;
        lock (server.TreeGate)
        {
            snapshot = server.Tree.NodesByPath.Values
                .OrderBy(static node => node.Path, StringComparer.Ordinal)
                .ToArray();
        }

        var items = new List<AtSpiAccessibleCacheItem>(snapshot.Length);
        items.AddRange(snapshot.Select(t => server.BuildCacheItem(t)));

        return ValueTask.FromResult(items);
    }

    public void EmitAddAccessibleSignal(AtSpiAccessibleCacheItem item)
    {
        EmitSignal("AddAccessible", item);
    }

    public void EmitRemoveAccessibleSignal(AtSpiObjectReference node)
    {
        EmitSignal("RemoveAccessible", node);
    }

    private void EmitSignal(string member, params object[] body)
    {
        var message = DBusMessage.CreateSignal(
            (DBusObjectPath)CachePath,
            IfaceCache,
            member,
            body);

        _ = server.A11yConnection.SendMessageAsync(message);
    }
}
