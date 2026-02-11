using Avalonia.DBus;
using Avalonia.DBus.SourceGen;

namespace Atspi2TestApp;

internal sealed class NodeHandlers(AccessibleNode node)
{
    private const string PropertiesInterfaceName = "org.freedesktop.DBus.Properties";
    private const string ErrorUnknownMethod = "org.freedesktop.DBus.Error.UnknownMethod";
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";
    private const string ErrorInvalidArgs = "org.freedesktop.DBus.Error.InvalidArgs";

    public AccessibleNode Node { get; } = node;

    public AccessibleHandler? AccessibleHandler { get; set; }
    public ApplicationHandler? ApplicationHandler { get; set; }
    public ComponentHandler? ComponentHandler { get; set; }
    public ActionHandler? ActionHandler { get; set; }
    public ValueHandler? ValueHandler { get; set; }
    public EventObjectHandler? EventObjectHandler { get; set; }

    public IDisposable Register(
        IDBusConnection connection,
        SynchronizationContext? synchronizationContext = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var path = (DBusObjectPath)Node.Path;
        List<IDisposable> registrations = [];

        try
        {
            if (AccessibleHandler != null)
                registrations.Add(OrgA11yAtspiAccessibleExport.Register(connection, path, AccessibleHandler, synchronizationContext));

            if (ApplicationHandler != null)
                registrations.Add(OrgA11yAtspiApplicationExport.Register(connection, path, ApplicationHandler, synchronizationContext));

            if (ComponentHandler != null)
                registrations.Add(OrgA11yAtspiComponentExport.Register(connection, path, ComponentHandler, synchronizationContext));

            if (ActionHandler != null)
                registrations.Add(OrgA11yAtspiActionExport.Register(connection, path, ActionHandler, synchronizationContext));

            if (ValueHandler != null)
                registrations.Add(OrgA11yAtspiValueExport.Register(connection, path, ValueHandler, synchronizationContext));

            if (EventObjectHandler != null)
                registrations.Add(OrgA11yAtspiEventObjectExport.Register(connection, path, EventObjectHandler, synchronizationContext));

            registrations.Add(connection.RegisterObject(path, PropertiesInterfaceName, HandlePropertiesAsync, synchronizationContext));

            return new CompositeRegistration(registrations);
        }
        catch
        {
            for (var i = 0; i < registrations.Count; i++)
                registrations[i].Dispose();

            throw;
        }
    }

    private Task<DBusMessage> HandlePropertiesAsync(IDBusConnection _, DBusMessage message)
    {
        try
        {
            switch (message.Member)
            {
                case "Get":
                {
                    if (message.Body.Count < 2 || message.Body[0] is not string iface || message.Body[1] is not string propertyName)
                        return Task.FromResult(message.CreateError(ErrorInvalidArgs, "Invalid Get arguments."));

                    if (!TryResolveTarget(iface, out var target))
                        return Task.FromResult(message.CreateError(ErrorUnknownInterface, "Unknown interface"));

                    if (!TryGetProperty(iface, target, propertyName, out var value))
                        return Task.FromResult(message.CreateError(ErrorUnknownProperty, "Unknown property"));

                    return Task.FromResult(message.CreateReply(value));
                }
                case "GetAll":
                {
                    if (message.Body.Count < 1 || message.Body[0] is not string iface)
                        return Task.FromResult(message.CreateError(ErrorInvalidArgs, "Invalid GetAll arguments."));

                    if (!TryResolveTarget(iface, out var target))
                        return Task.FromResult(message.CreateError(ErrorUnknownInterface, "Unknown interface"));

                    return Task.FromResult(message.CreateReply(GetAllProperties(iface, target)));
                }
                case "Set":
                {
                    if (message.Body.Count < 3 || message.Body[0] is not string iface || message.Body[1] is not string propertyName || message.Body[2] is not DBusVariant value)
                        return Task.FromResult(message.CreateError(ErrorInvalidArgs, "Invalid Set arguments."));

                    if (!TryResolveTarget(iface, out var target))
                        return Task.FromResult(message.CreateError(ErrorUnknownInterface, "Unknown interface"));

                    if (!TrySetProperty(iface, target, propertyName, value))
                        return Task.FromResult(message.CreateError(ErrorUnknownProperty, "Unknown property"));

                    return Task.FromResult(message.CreateReply());
                }
                default:
                    return Task.FromResult(message.CreateError(ErrorUnknownMethod, "Unknown method"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(message.CreateError(ErrorInvalidArgs, ex.Message));
        }
    }

    private bool TryResolveTarget(string iface, out object target)
    {
        switch (iface)
        {
            case Program.IfaceAccessible when AccessibleHandler != null:
                target = AccessibleHandler;
                return true;
            case Program.IfaceApplication when ApplicationHandler != null:
                target = ApplicationHandler;
                return true;
            case Program.IfaceComponent when ComponentHandler != null:
                target = ComponentHandler;
                return true;
            case Program.IfaceAction when ActionHandler != null:
                target = ActionHandler;
                return true;
            case Program.IfaceValue when ValueHandler != null:
                target = ValueHandler;
                return true;
            case Program.IfaceEventObject when EventObjectHandler != null:
                target = EventObjectHandler;
                return true;
            default:
                target = null!;
                return false;
        }
    }

    private static bool TryGetProperty(string iface, object target, string propertyName, out DBusVariant value)
    {
        switch (iface)
        {
            case Program.IfaceAccessible:
                return OrgA11yAtspiAccessibleExport.TryGetProperty(target, propertyName, out value);
            case Program.IfaceApplication:
                return OrgA11yAtspiApplicationExport.TryGetProperty(target, propertyName, out value);
            case Program.IfaceComponent:
                return OrgA11yAtspiComponentExport.TryGetProperty(target, propertyName, out value);
            case Program.IfaceAction:
                return OrgA11yAtspiActionExport.TryGetProperty(target, propertyName, out value);
            case Program.IfaceValue:
                return OrgA11yAtspiValueExport.TryGetProperty(target, propertyName, out value);
            case Program.IfaceEventObject:
                return OrgA11yAtspiEventObjectExport.TryGetProperty(target, propertyName, out value);
            default:
                value = default!;
                return false;
        }
    }

    private static bool TrySetProperty(string iface, object target, string propertyName, DBusVariant value)
    {
        switch (iface)
        {
            case Program.IfaceAccessible:
                return OrgA11yAtspiAccessibleExport.TrySetProperty(target, propertyName, value);
            case Program.IfaceApplication:
                return OrgA11yAtspiApplicationExport.TrySetProperty(target, propertyName, value);
            case Program.IfaceComponent:
                return OrgA11yAtspiComponentExport.TrySetProperty(target, propertyName, value);
            case Program.IfaceAction:
                return OrgA11yAtspiActionExport.TrySetProperty(target, propertyName, value);
            case Program.IfaceValue:
                return OrgA11yAtspiValueExport.TrySetProperty(target, propertyName, value);
            case Program.IfaceEventObject:
                return OrgA11yAtspiEventObjectExport.TrySetProperty(target, propertyName, value);
            default:
                return false;
        }
    }

    private static Dictionary<string, DBusVariant> GetAllProperties(string iface, object target)
    {
        return iface switch
        {
            Program.IfaceAccessible => OrgA11yAtspiAccessibleExport.GetAllProperties(target),
            Program.IfaceApplication => OrgA11yAtspiApplicationExport.GetAllProperties(target),
            Program.IfaceComponent => OrgA11yAtspiComponentExport.GetAllProperties(target),
            Program.IfaceAction => OrgA11yAtspiActionExport.GetAllProperties(target),
            Program.IfaceValue => OrgA11yAtspiValueExport.GetAllProperties(target),
            Program.IfaceEventObject => OrgA11yAtspiEventObjectExport.GetAllProperties(target),
            _ => new Dictionary<string, DBusVariant>(StringComparer.Ordinal)
        };
    }

    private sealed class CompositeRegistration(IReadOnlyList<IDisposable> registrations) : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _registrations = registrations;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            for (var i = 0; i < _registrations.Count; i++)
                _registrations[i].Dispose();
        }
    }
}
