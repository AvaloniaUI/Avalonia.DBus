using Avalonia.DBus.Wire;
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

    public override Connection Connection => _server.A11yConnection;

    public void EmitChildrenChangedSignal(string operation, int indexInParent, VariantValue child)
    {
        EmitChildrenChanged(operation, indexInParent, 0, child, null);
    }

    public void EmitPropertyChangeSignal(string propertyName, VariantValue value)
    {
        EmitPropertyChange(propertyName, 0, 0, value, null);
    }
}
