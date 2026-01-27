using Avalonia.DBus.SourceGen;

namespace Atspi2TestApp;

internal sealed class NodeHandlers
{
    public NodeHandlers(AccessibleNode node, PathHandler pathHandler)
    {
        Node = node;
        PathHandler = pathHandler;
    }

    public AccessibleNode Node { get; }
    public PathHandler PathHandler { get; }

    public AccessibleHandler? AccessibleHandler { get; set; }
    public ApplicationHandler? ApplicationHandler { get; set; }
    public ComponentHandler? ComponentHandler { get; set; }
    public ActionHandler? ActionHandler { get; set; }
    public ValueHandler? ValueHandler { get; set; }
    public EventObjectHandler? EventObjectHandler { get; set; }

    public void Add(IDBusInterfaceHandler handler)
    {
        PathHandler.Add(handler);
    }
}
