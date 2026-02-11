using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.DBus;

public sealed class DBusBuiltIns
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

    private string? _machineId;

    public bool EnablePeerInterface { get; set; } = true;
    public bool EnablePropertiesInterface { get; set; } = true;
    public bool EnableIntrospectableInterface { get; set; } = true;

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

    internal DBusMessage? TryHandleProperties(DBusMessage request, IDBusObject obj)
    {
        if (!EnablePropertiesInterface || request.Type != DBusMessageType.MethodCall)
            return null;

        if (!string.Equals(request.Interface, PropertiesInterfaceName, StringComparison.Ordinal))
            return null;

        switch (request.Member)
        {
            case "Get":
                if (request.Body.Count < 2 || request.Body[0] is not string iface || request.Body[1] is not string name)
                    return request.CreateError(ErrorUnknownMethod, "Invalid Get arguments.");

                if (!HasInterface(obj, iface))
                    return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                if (!obj.TryGetProperty(iface, name, out var value))
                    return request.CreateError(ErrorUnknownProperty, "Unknown property");

                return request.CreateReply(value);

            case "GetAll":
                if (request.Body.Count < 1 || request.Body[0] is not string getAllIface)
                    return request.CreateError(ErrorUnknownMethod, "Invalid GetAll arguments.");

                if (!HasInterface(obj, getAllIface))
                    return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                return obj.TryGetAllProperties(getAllIface, out var props)
                    ? request.CreateReply(props)
                    : request.CreateError(ErrorUnknownProperty, "Unknown property");

            case "Set":
                if (request.Body.Count < 3 || request.Body[0] is not string setIface || request.Body[1] is not string setName || request.Body[2] is not DBusVariant setValue)
                    return request.CreateError(ErrorUnknownMethod, "Invalid Set arguments.");

                if (!HasInterface(obj, setIface))
                    return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                if (!obj.TrySetProperty(setIface, setName, setValue))
                    return request.CreateError(ErrorUnknownProperty, "Unknown property");

                return request.CreateReply();

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

    private static bool HasInterface(IDBusObject obj, string iface)
    {
        if (!obj.TryGetInterfaces(out var ifaces))
            return false;

        foreach (var item in ifaces)
        {
            if (string.Equals(item, iface, StringComparison.Ordinal))
                return true;
        }

        return false;
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
