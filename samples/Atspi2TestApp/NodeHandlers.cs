using Avalonia.DBus;

namespace Atspi2TestApp;

internal sealed class NodeHandlers(AccessibleNode node, DBusObject dbusObject)
{
    public AccessibleNode Node { get; } = node;
    public DBusObject DbusObject { get; } = dbusObject;

    public AccessibleHandler? AccessibleHandler { get; set; }
    public ApplicationHandler? ApplicationHandler { get; set; }
    public ComponentHandler? ComponentHandler { get; set; }
    public ActionHandler? ActionHandler { get; set; }
    public ValueHandler? ValueHandler { get; set; }
    public EventObjectHandler? EventObjectHandler { get; set; }

    public void Add(IDBusInterfaceHandler handler)
    {
        DbusObject.Add(handler);
    }
}
