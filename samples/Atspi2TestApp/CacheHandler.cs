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

    protected override ValueTask<DBusArray<DBusStruct>> OnGetItemsAsync(DBusMessage request)
    {
        AccessibleNode[] snapshot;
        lock (_server.TreeGate)
        {
            snapshot = _server.Tree.NodesByPath.Values
                .OrderBy(static node => node.Path, StringComparer.Ordinal)
                .ToArray();
        }

        var items = new DBusStruct[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            items[i] = _server.BuildCacheItem(snapshot[i]);
        }

        return ValueTask.FromResult(
            items.Length == 0
                ? new DBusArray<DBusStruct>("((so)(so)(so)iiassusau)")
                : new DBusArray<DBusStruct>(items));
    }

    public void EmitAddAccessibleSignal(DBusStruct item)
    {
        EmitAddAccessible(item);
    }

    public void EmitRemoveAccessibleSignal(DBusStruct node)
    {
        EmitRemoveAccessible(node);
    }
}
