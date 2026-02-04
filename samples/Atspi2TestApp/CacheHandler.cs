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

    protected override ValueTask<DBusArray<DbusStruct_Rrsozrsozrsoziiassusauz>> OnGetItemsAsync(DBusMessage request)
    {
        AccessibleNode[] snapshot;
        lock (_server.TreeGate)
        {
            snapshot = _server.Tree.NodesByPath.Values
                .OrderBy(static node => node.Path, StringComparer.Ordinal)
                .ToArray();
        }

        var items = new DbusStruct_Rrsozrsozrsoziiassusauz[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            items[i] = _server.BuildCacheItem(snapshot[i]);
        }

        return ValueTask.FromResult(
            items.Length == 0
                ? new DBusArray<DbusStruct_Rrsozrsozrsoziiassusauz>(DbusStruct_Rrsozrsozrsoziiassusauz.Signature)
                : new DBusArray<DbusStruct_Rrsozrsozrsoziiassusauz>(DbusStruct_Rrsozrsozrsoziiassusauz.Signature, items));
    }

    public void EmitAddAccessibleSignal(DbusStruct_Rrsozrsozrsoziiassusauz item)
    {
        EmitAddAccessible(item);
    }

    public void EmitRemoveAccessibleSignal(DbusStruct_Rsoz node)
    {
        EmitRemoveAccessible(node);
    }
}
