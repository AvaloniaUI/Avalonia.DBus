using Atspi2TestApp.DBusXml;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ComponentHandler(AtspiServer server, AccessibleNode node) : IOrgA11yAtspiComponent
{
    public uint Version => ComponentVersion;

    public ValueTask<bool> ContainsAsync(int x, int y, uint coordType)
    {
        var screenPoint = server.TranslatePoint(node, x, y, coordType);
        var contains = server.ContainsPoint(node.Extents, screenPoint.x, screenPoint.y);
        return ValueTask.FromResult(contains);
    }

    public ValueTask<AtSpiObjectReference> GetAccessibleAtPointAsync(int x, int y, uint coordType)
    {
        var screenPoint = server.TranslatePoint(node, x, y, coordType);
        var target = server.FindAtPoint(node, screenPoint.x, screenPoint.y);
        return ValueTask.FromResult(server.GetReference(target));
    }

    public ValueTask<AtSpiRect> GetExtentsAsync(uint coordType)
    {
        var rect = server.TranslateRect(node, coordType);
        return ValueTask.FromResult(new AtSpiRect(rect.X, rect.Y, rect.Width, rect.Height));
    }

    public ValueTask<(int X, int Y)> GetPositionAsync(uint coordType)
    {
        var rect = server.TranslateRect(node, coordType);
        return ValueTask.FromResult((rect.X, rect.Y));
    }

    public ValueTask<(int Width, int Height)> GetSizeAsync()
    {
        return ValueTask.FromResult((node.Extents.Width, node.Extents.Height));
    }

    public ValueTask<uint> GetLayerAsync()
    {
        var layer = node.Role == RoleFrame ? 7u : 3u;
        return ValueTask.FromResult(layer);
    }

    public ValueTask<short> GetMDIZOrderAsync() => ValueTask.FromResult((short)-1);

    public ValueTask<bool> GrabFocusAsync()
    {
        server.SetFocused(node);
        return ValueTask.FromResult(true);
    }

    public ValueTask<double> GetAlphaAsync() => ValueTask.FromResult(1.0);

    public ValueTask<bool> SetExtentsAsync(int x, int y, int width, int height, uint coordType)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> SetPositionAsync(int x, int y, uint coordType)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> SetSizeAsync(int width, int height)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> ScrollToAsync(uint type)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> ScrollToPointAsync(uint coordType, int x, int y)
    {
        return ValueTask.FromResult(false);
    }
}
