using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ApplicationHandler : OrgA11yAtspiApplicationHandler
{
    private readonly AtspiServer _server;
    private readonly AccessibleNode _node;

    public ApplicationHandler(AtspiServer server, AccessibleNode node)
    {
        _server = server;
        _node = node;
        var version = ResolveToolkitVersion();
        ToolkitName = "Avalonia";
        Version = version;
        ToolkitVersion = version;
        AtspiVersion = "2.1";
        InterfaceVersion = ApplicationVersion;
    }

    public override DBusConnection Connection => _server.A11yConnection;

    public override int Id
    {
        get => _node.ApplicationId ?? 0;
        set => _node.ApplicationId = value;
    }

    protected override ValueTask<string> OnGetLocaleAsync(DBusMessage request, uint lctype)
    {
        return ValueTask.FromResult(ResolveLocale());
    }

    protected override ValueTask<string> OnGetApplicationBusAddressAsync(DBusMessage request)
    {
        return ValueTask.FromResult(string.Empty);
    }
}
