using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Avalonia.DBus;

internal sealed class BuiltInPropertiesHandler(
    IReadOnlyDictionary<string, BoundProperties> propertiesByInterface)
    : IDBusInterfaceCallDispatcher
{
    private const string ErrorUnknownMethod = "org.freedesktop.DBus.Error.UnknownMethod";
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";
    private const string ErrorInvalidArgs = "org.freedesktop.DBus.Error.InvalidArgs";

    public const string InterfaceName = "org.freedesktop.DBus.Properties";

    public Task<DBusMessage> Handle(IDBusConnection _, DBusMessage message)
    {
        try
        {
            return message.Member switch
            {
                "Get" => HandleGet(message),
                "GetAll" => HandleGetAll(message),
                "Set" => HandleSet(message),
                _ => Task.FromResult(message.CreateError(ErrorUnknownMethod, "Unknown method"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(message.CreateError(ErrorInvalidArgs, ex.Message));
        }
    }

    private Task<DBusMessage> HandleGet(DBusMessage message)
    {
        if (message.Body.Count < 2 || message.Body[0] is not string iface || message.Body[1] is not string propertyName)
            return Task.FromResult(message.CreateError(ErrorInvalidArgs, "Invalid Get arguments."));

        if (!propertiesByInterface.TryGetValue(iface, out var properties))
            return Task.FromResult(message.CreateError(ErrorUnknownInterface, "Unknown interface"));

        if (properties.TryGet == null)
            return Task.FromResult(message.CreateError(ErrorUnknownProperty, "Unknown property"));

        var value = properties.TryGet(propertyName);
        return value == null
            ? Task.FromResult(message.CreateError(ErrorUnknownProperty, "Unknown property"))
            : Task.FromResult(message.CreateReply(value));
    }

    private Task<DBusMessage> HandleGetAll(DBusMessage message)
    {
        if (message.Body.Count < 1 || message.Body[0] is not string iface)
            return Task.FromResult(message.CreateError(ErrorInvalidArgs, "Invalid GetAll arguments."));

        if (!propertiesByInterface.TryGetValue(iface, out var properties))
            return Task.FromResult(message.CreateError(ErrorUnknownInterface, "Unknown interface"));

        if (properties.GetAll != null)
            return Task.FromResult(message.CreateReply(properties.GetAll()));

        return Task.FromResult(message.CreateReply(new Dictionary<string, DBusVariant>(StringComparer.Ordinal)));
    }

    private Task<DBusMessage> HandleSet(DBusMessage message)
    {
        if (message.Body.Count < 3 || message.Body[0] is not string iface || message.Body[1] is not string propertyName || message.Body[2] is not DBusVariant value)
            return Task.FromResult(message.CreateError(ErrorInvalidArgs, "Invalid Set arguments."));

        if (!propertiesByInterface.TryGetValue(iface, out var properties))
            return Task.FromResult(message.CreateError(ErrorUnknownInterface, "Unknown interface"));

        if (properties.TrySet == null || !properties.TrySet(propertyName, value))
            return Task.FromResult(message.CreateError(ErrorUnknownProperty, "Unknown property"));

        return Task.FromResult(message.CreateReply());
    }
}
