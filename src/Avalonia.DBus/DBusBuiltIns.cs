using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Avalonia.DBus;

internal sealed class DBusBuiltIns
{
    private const string PeerInterfaceName = "org.freedesktop.DBus.Peer";
    private const string PropertiesInterfaceName = "org.freedesktop.DBus.Properties";
    private const string IntrospectableInterfaceName = "org.freedesktop.DBus.Introspectable";

    internal const string PropertiesIntrospectXml =
        "<interface name=\"org.freedesktop.DBus.Properties\">\n  <method name=\"Get\">\n    <arg name=\"interface\" type=\"s\" direction=\"in\"/>\n    <arg name=\"property\" type=\"s\" direction=\"in\"/>\n    <arg name=\"value\" type=\"v\" direction=\"out\"/>\n  </method>\n  <method name=\"Set\">\n    <arg name=\"interface\" type=\"s\" direction=\"in\"/>\n    <arg name=\"property\" type=\"s\" direction=\"in\"/>\n    <arg name=\"value\" type=\"v\" direction=\"in\"/>\n  </method>\n  <method name=\"GetAll\">\n    <arg name=\"interface\" type=\"s\" direction=\"in\"/>\n    <arg name=\"properties\" type=\"a{sv}\" direction=\"out\"/>\n  </method>\n  <signal name=\"PropertiesChanged\">\n    <arg name=\"interface\" type=\"s\"/>\n    <arg name=\"changed_properties\" type=\"a{sv}\"/>\n    <arg name=\"invalidated_properties\" type=\"as\"/>\n  </signal>\n</interface>\n";

    internal const string IntrospectableIntrospectXml =
        "<interface name=\"org.freedesktop.DBus.Introspectable\">\n  <method name=\"Introspect\">\n    <arg name=\"xml_data\" type=\"s\" direction=\"out\"/>\n  </method>\n</interface>\n";

    internal const string PeerIntrospectXml =
        "<interface name=\"org.freedesktop.DBus.Peer\">\n  <method name=\"Ping\" />\n  <method name=\"GetMachineId\">\n    <arg name=\"machine_uuid\" type=\"s\" direction=\"out\" />\n  </method>\n</interface>\n";

    private const string ErrorUnknownMethod = "org.freedesktop.DBus.Error.UnknownMethod";
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";
    private const string ErrorInvalidArgs = "org.freedesktop.DBus.Error.InvalidArgs";

    private string? _machineId;

    public bool EnablePeerInterface { get; set; } = true;
    public bool EnablePropertiesInterface { get; set; } = true;
    public bool EnableIntrospectableInterface { get; set; } = true;

    internal static bool IsBuiltInInterface(string? iface)
    {
        if (string.IsNullOrEmpty(iface))
            return false;

        return string.Equals(iface, PeerInterfaceName, StringComparison.Ordinal)
               || string.Equals(iface, PropertiesInterfaceName, StringComparison.Ordinal)
               || string.Equals(iface, IntrospectableInterfaceName, StringComparison.Ordinal);
    }

    internal DBusMessage? TryHandlePeer(DBusMessage request)
    {
        if (!EnablePeerInterface || request.Type != DBusMessageType.MethodCall)
            return null;

        if (!string.Equals(request.Interface, PeerInterfaceName, StringComparison.Ordinal))
            return null;

        return request.Member switch
        {
            "Ping" => request.CreateReply(),
            "GetMachineId" => request.CreateReply(GetMachineId()),
            _ => request.CreateError(ErrorUnknownMethod, "Unknown method")
        };
    }

    internal DBusMessage? TryHandleProperties(DBusMessage request, DBusRegisteredPathEntry? entry)
    {
        if (!EnablePropertiesInterface || request.Type != DBusMessageType.MethodCall)
            return null;

        if (!string.Equals(request.Interface, PropertiesInterfaceName, StringComparison.Ordinal))
            return null;

        switch (request.Member)
        {
            case "Get":
                if (request.Body.Count < 2 || request.Body[0] is not string iface || request.Body[1] is not string name)
                    return request.CreateError(ErrorInvalidArgs, "Invalid Get arguments.");

                if (!TryGetBoundInterface(entry, iface, out var getBinding))
                    return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                if (!getBinding.Descriptor.Properties.TryGetValue(name, out var getProperty)
                    || !getProperty.CanRead
                    || getProperty.TryGet == null)
                {
                    return request.CreateError(ErrorUnknownProperty, "Unknown property");
                }

                try
                {
                    if (!getProperty.TryGet(getBinding.Target, out var value))
                        return request.CreateError(ErrorUnknownProperty, "Unknown property");

                    return request.CreateReply(value);
                }
                catch (Exception ex)
                {
                    return request.CreateError(ErrorInvalidArgs, ex.Message);
                }

            case "GetAll":
                if (request.Body.Count < 1 || request.Body[0] is not string getAllIface)
                    return request.CreateError(ErrorInvalidArgs, "Invalid GetAll arguments.");

                if (!TryGetBoundInterface(entry, getAllIface, out var getAllBinding))
                    return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                var values = new Dictionary<string, DBusVariant>(StringComparer.Ordinal);
                foreach (var (propertyName, descriptor) in getAllBinding.Descriptor.Properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!descriptor.CanRead || descriptor.TryGet == null)
                        continue;

                    try
                    {
                        if (descriptor.TryGet(getAllBinding.Target, out var value))
                            values[propertyName] = value;
                    }
                    catch (Exception ex)
                    {
                        return request.CreateError(ErrorInvalidArgs, ex.Message);
                    }
                }

                return request.CreateReply(values);

            case "Set":
                if (request.Body.Count < 3 || request.Body[0] is not string setIface || request.Body[1] is not string setName || request.Body[2] is not DBusVariant setValue)
                    return request.CreateError(ErrorInvalidArgs, "Invalid Set arguments.");

                if (!TryGetBoundInterface(entry, setIface, out var setBinding))
                    return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                if (!setBinding.Descriptor.Properties.TryGetValue(setName, out var setProperty)
                    || !setProperty.CanWrite
                    || setProperty.TrySet == null)
                {
                    return request.CreateError(ErrorUnknownProperty, "Unknown property");
                }

                try
                {
                    if (!setProperty.TrySet(setBinding.Target, setValue))
                        return request.CreateError(ErrorUnknownProperty, "Unknown property");

                    return request.CreateReply();
                }
                catch (Exception ex)
                {
                    return request.CreateError(ErrorInvalidArgs, ex.Message);
                }

            default:
                return request.CreateError(ErrorUnknownMethod, "Unknown method");
        }
    }

    internal DBusMessage? TryHandleIntrospectable(DBusMessage request, string xmlData)
    {
        if (!EnableIntrospectableInterface || request.Type != DBusMessageType.MethodCall)
            return null;

        if (!string.Equals(request.Interface, IntrospectableInterfaceName, StringComparison.Ordinal))
            return null;

        return request.Member switch
        {
            "Introspect" => request.CreateReply(xmlData),
            _ => request.CreateError(ErrorUnknownMethod, "Unknown method")
        };
    }

    private static bool TryGetBoundInterface(
        DBusRegisteredPathEntry? entry,
        string iface,
        out DBusBoundInterfaceRegistration binding)
    {
        if (entry == null || string.IsNullOrEmpty(iface))
        {
            binding = null!;
            return false;
        }

        return entry.TryGetBinding(iface, out binding);
    }

    private string GetMachineId()
    {
        if (!string.IsNullOrEmpty(_machineId))
            return _machineId!;

        string[] paths = ["/etc/machine-id", "/var/lib/dbus/machine-id"];
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var text = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    _machineId = text;
                    return text;
                }
            }
            catch
            {
                // ignored
            }
        }

        _machineId = "unknown";
        return _machineId;
    }
}
