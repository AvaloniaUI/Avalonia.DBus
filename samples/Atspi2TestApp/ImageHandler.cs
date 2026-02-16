using Atspi2TestApp.DBusXml;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ImageHandler(AtspiServer server, AccessibleNode node) : IOrgA11yAtspiImage
{
    public uint Version => ImageVersion;
    public string ImageDescription => node.Description;
    public string ImageLocale => node.Locale;

    public ValueTask<AtSpiRect> GetImageExtentsAsync(uint coordType)
    {
        var rect = server.TranslateRect(node, coordType);
        return ValueTask.FromResult(new AtSpiRect(rect.X, rect.Y, rect.Width, rect.Height));
    }

    public ValueTask<(int X, int Y)> GetImagePositionAsync(uint coordType)
    {
        var rect = server.TranslateRect(node, coordType);
        return ValueTask.FromResult((rect.X, rect.Y));
    }

    public ValueTask<(int Width, int Height)> GetImageSizeAsync()
    {
        return ValueTask.FromResult((node.Extents.Width, node.Extents.Height));
    }
}
