using System.Threading.Tasks;
using Avalonia.DBus;

namespace Atspi2TestApp;

internal sealed class NodeHandlers(AccessibleNode node)
{
    public AccessibleNode Node { get; } = node;
    public AccessibleHandler? AccessibleHandler { get; set; }
    public ApplicationHandler? ApplicationHandler { get; set; }
    public ComponentHandler? ComponentHandler { get; set; }
    public ActionHandler? ActionHandler { get; set; }
    public ValueHandler? ValueHandler { get; set; }
    public ImageHandler? ImageHandler { get; set; }
    public EventObjectHandler? EventObjectHandler { get; set; }

    public async Task<IDisposable> Register(
        IDBusConnection connection,
        SynchronizationContext? synchronizationContext = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        List<object> targets = [];
        if (AccessibleHandler != null)
            targets.Add(AccessibleHandler);
        if (ApplicationHandler != null)
            targets.Add(ApplicationHandler);
        if (ComponentHandler != null)
            targets.Add(ComponentHandler);
        if (ActionHandler != null)
            targets.Add(ActionHandler);
        if (ValueHandler != null)
            targets.Add(ValueHandler);
        if (ImageHandler != null)
            targets.Add(ImageHandler);
        if (EventObjectHandler != null)
            targets.Add(EventObjectHandler);

        if (targets.Count == 0)
            return EmptyRegistration.Instance;

        return await connection.RegisterObjects((DBusObjectPath)Node.Path, targets, synchronizationContext);
    }

    private sealed class EmptyRegistration : IDisposable
    {
        public static EmptyRegistration Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
