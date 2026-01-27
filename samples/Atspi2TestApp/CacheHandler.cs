using System.Linq;
using Avalonia.DBus.Wire;
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

    public override Connection Connection => _server.A11yConnection;

    protected override ValueTask<((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, string, uint[])[]>
        OnGetItemsAsync(Message request)
    {
        AccessibleNode[] snapshot;
        lock (_server.TreeGate)
        {
            snapshot = _server.Tree.NodesByPath.Values
                .OrderBy(static node => node.Path, StringComparer.Ordinal)
                .ToArray();
        }

        var items = new ((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, string, uint[])[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            items[i] = _server.BuildCacheItem(snapshot[i]);
        }

        return ValueTask.FromResult(items);
    }

    public void EmitAddAccessibleSignal(((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, string, uint[]) item)
    {
        EmitAddAccessible(item);
    }

    public void EmitRemoveAccessibleSignal((string, ObjectPath) node)
    {
        EmitRemoveAccessible(node);
    }
}
