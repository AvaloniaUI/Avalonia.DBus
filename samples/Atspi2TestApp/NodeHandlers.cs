using Avalonia.DBus;
using Avalonia.DBus.SourceGen;

namespace Atspi2TestApp;

internal sealed class NodeHandlers(AccessibleNode node)
{
    public AccessibleNode Node { get; } = node;

    public AccessibleHandler? AccessibleHandler { get; set; }
    public ApplicationHandler? ApplicationHandler { get; set; }
    public ComponentHandler? ComponentHandler { get; set; }
    public ActionHandler? ActionHandler { get; set; }
    public ValueHandler? ValueHandler { get; set; }
    public EventObjectHandler? EventObjectHandler { get; set; }

    public DBusExportedTarget CreateExportedTarget()
    {
        return DBusExportedTarget.Create(
            Node,
            builder =>
            {
                if (AccessibleHandler != null)
                    OrgA11yAtspiAccessibleExport.Bind(builder, AccessibleHandler);

                if (ApplicationHandler != null)
                    OrgA11yAtspiApplicationExport.Bind(builder, ApplicationHandler);

                if (ComponentHandler != null)
                    OrgA11yAtspiComponentExport.Bind(builder, ComponentHandler);

                if (ActionHandler != null)
                    OrgA11yAtspiActionExport.Bind(builder, ActionHandler);

                if (ValueHandler != null)
                    OrgA11yAtspiValueExport.Bind(builder, ValueHandler);

                if (EventObjectHandler != null)
                    OrgA11yAtspiEventObjectExport.Bind(builder, EventObjectHandler);
            });
    }
}
