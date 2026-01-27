using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ComponentHandler : OrgA11yAtspiComponentHandler
{
    private readonly AtspiServer _server;
    private readonly AccessibleNode _node;

    public ComponentHandler(AtspiServer server, AccessibleNode node)
    {
        _server = server;
        _node = node;
        Version = ComponentVersion;
    }

    public override DBusConnection Connection => _server.A11yConnection;

    protected override ValueTask<bool> OnContainsAsync(DBusMessage request, int x, int y, uint coordType)
    {
        var screenPoint = _server.TranslatePoint(_node, x, y, coordType);
        var contains = _server.ContainsPoint(_node.Extents, screenPoint.x, screenPoint.y);
        return ValueTask.FromResult(contains);
    }

    protected override ValueTask<DBusStruct> OnGetAccessibleAtPointAsync(DBusMessage request, int x, int y, uint coordType)
    {
        var screenPoint = _server.TranslatePoint(_node, x, y, coordType);
        var target = _server.FindAtPoint(_node, screenPoint.x, screenPoint.y);
        return ValueTask.FromResult(_server.GetReference(target));
    }

    protected override ValueTask<DBusStruct> OnGetExtentsAsync(DBusMessage request, uint coordType)
    {
        var rect = _server.TranslateRect(_node, coordType);
        return ValueTask.FromResult(new DBusStruct(rect.X, rect.Y, rect.Width, rect.Height));
    }

    protected override ValueTask<(int X, int Y)> OnGetPositionAsync(DBusMessage request, uint coordType)
    {
        var rect = _server.TranslateRect(_node, coordType);
        return ValueTask.FromResult((rect.X, rect.Y));
    }

    protected override ValueTask<(int Width, int Height)> OnGetSizeAsync(DBusMessage request)
    {
        return ValueTask.FromResult((_node.Extents.Width, _node.Extents.Height));
    }

    protected override ValueTask<uint> OnGetLayerAsync(DBusMessage request)
    {
        var layer = _node.Role == RoleFrame ? 7u : 3u;
        return ValueTask.FromResult(layer);
    }

    protected override ValueTask<short> OnGetMDIZOrderAsync(DBusMessage request)
    {
        return ValueTask.FromResult((short)-1);
    }

    protected override ValueTask<bool> OnGrabFocusAsync(DBusMessage request)
    {
        _server.SetFocused(_node);
        return ValueTask.FromResult(true);
    }

    protected override ValueTask<double> OnGetAlphaAsync(DBusMessage request)
    {
        return ValueTask.FromResult(1.0);
    }

    protected override ValueTask<bool> OnSetExtentsAsync(DBusMessage request, int x, int y, int width, int height, uint coordType)
    {
        return ValueTask.FromResult(false);
    }

    protected override ValueTask<bool> OnSetPositionAsync(DBusMessage request, int x, int y, uint coordType)
    {
        return ValueTask.FromResult(false);
    }

    protected override ValueTask<bool> OnSetSizeAsync(DBusMessage request, int width, int height)
    {
        return ValueTask.FromResult(false);
    }

    protected override ValueTask<bool> OnScrollToAsync(DBusMessage request, uint type)
    {
        return ValueTask.FromResult(false);
    }

    protected override ValueTask<bool> OnScrollToPointAsync(DBusMessage request, uint type, int x, int y)
    {
        return ValueTask.FromResult(false);
    }
}
