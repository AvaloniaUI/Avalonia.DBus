using Avalonia.DBus;
using Avalonia.DBus.SourceGen;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class EventObjectHandler(AtspiServer server, string path) : IOrgA11yAtspiEventObject
{
    public uint Version => EventObjectVersion;

    public void EmitChildrenChangedSignal(string operation, int indexInParent, DBusVariant child)
    {
        EmitSignal("ChildrenChanged", operation, indexInParent, 0, child, EmptyProperties());
    }

    public void EmitPropertyChangeSignal(string propertyName, DBusVariant value)
    {
        EmitSignal("PropertyChange", propertyName, 0, 0, value, EmptyProperties());
    }

    private void EmitSignal(string member, params object[] body)
    {
        var message = DBusMessage.CreateSignal(
            (DBusObjectPath)path,
            IfaceEventObject,
            member,
            body);

        _ = server.A11yConnection.SendMessageAsync(message);
    }

    private static Dictionary<string, DBusVariant> EmptyProperties()
    {
        return new Dictionary<string, DBusVariant>(StringComparer.Ordinal);
    }
}
