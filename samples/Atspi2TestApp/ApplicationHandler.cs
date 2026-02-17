using Atspi2TestApp.DBusXml;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ApplicationHandler : IOrgA11yAtspiApplication
{
    private readonly AccessibleNode _node;

    public ApplicationHandler(AtspiServer server, AccessibleNode node)
    {
        _ = server;
        _node = node;
        var version = ResolveToolkitVersion();
        ToolkitName = "Avalonia";
        Version = version;
        ToolkitVersion = version;
        AtspiVersion = "2.1";
        InterfaceVersion = ApplicationVersion;
    }

    public string ToolkitName { get; }

    public string Version { get; }

    public string ToolkitVersion { get; }

    public string AtspiVersion { get; }

    public uint InterfaceVersion { get; }

    public int Id
    {
        get => _node.ApplicationId ?? 0;
        set => _node.ApplicationId = value;
    }

    public ValueTask<string> GetLocaleAsync(uint lctype)
    {
        return ValueTask.FromResult(ResolveLocale());
    }

    public ValueTask<string> GetApplicationBusAddressAsync()
    {
        return ValueTask.FromResult(string.Empty);
    }
}
