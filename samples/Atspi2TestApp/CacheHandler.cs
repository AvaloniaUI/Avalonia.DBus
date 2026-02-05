using Avalonia.DBus;
using Avalonia.DBus.SourceGen;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class CacheHandler : OrgA11yAtspiCacheHandler
{
    private readonly AtspiServer _server;

    public CacheHandler(AtspiServer server)
    {
        _server = server;
        Version = CacheVersion;
    }

    public override DBusConnection Connection => _server.A11yConnection;

    protected override ValueTask<List<AtSpiAccessibleCacheItem>> OnGetItemsAsync(DBusMessage request)
    {
        AccessibleNode[] snapshot;
        lock (_server.TreeGate)
        {
            snapshot = _server.Tree.NodesByPath.Values
                .OrderBy(static node => node.Path, StringComparer.Ordinal)
                .ToArray();
        }

        var items = new List<AtSpiAccessibleCacheItem>(snapshot.Length);
        items.AddRange(snapshot.Select(t => _server.BuildCacheItem(t)));

        return ValueTask.FromResult(items);
    }

    public void EmitAddAccessibleSignal(AtSpiAccessibleCacheItem item)
    {
        EmitAddAccessible(item);
    }

    public void EmitRemoveAccessibleSignal(AtSpiObjectReference node)
    {
        EmitRemoveAccessible(node);
    }
}
