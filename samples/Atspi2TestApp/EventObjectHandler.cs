using System.Collections.Generic;
using Avalonia.DBus;
using Avalonia.DBus.SourceGen;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class EventObjectHandler : OrgA11yAtspiEventObjectHandler
{
    private readonly AtspiServer _server;

    public EventObjectHandler(AtspiServer server)
    {
        _server = server;
        Version = EventObjectVersion;
    }

    public override DBusConnection Connection => _server.A11yConnection;

    public void EmitChildrenChangedSignal(string operation, int indexInParent, DBusVariant child)
    {
        EmitChildrenChanged(operation, indexInParent, 0, child, []);
    }

    public void EmitPropertyChangeSignal(string propertyName, DBusVariant value)
    {
        EmitPropertyChange(propertyName, 0, 0, value, []);
    }
}
