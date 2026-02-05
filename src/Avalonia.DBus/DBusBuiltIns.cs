using System;
using System.IO;

namespace Avalonia.DBus;

public static class DBusBuiltIns
{
    internal const string PropertiesIntrospectXml = "<interface name=\"org.freedesktop.DBus.Properties\">\n  <method name=\"Get\">\n    <arg name=\"interface\" type=\"s\" direction=\"in\"/>\n    <arg name=\"property\" type=\"s\" direction=\"in\"/>\n    <arg name=\"value\" type=\"v\" direction=\"out\"/>\n  </method>\n  <method name=\"Set\">\n    <arg name=\"interface\" type=\"s\" direction=\"in\"/>\n    <arg name=\"property\" type=\"s\" direction=\"in\"/>\n    <arg name=\"value\" type=\"v\" direction=\"in\"/>\n  </method>\n  <method name=\"GetAll\">\n    <arg name=\"interface\" type=\"s\" direction=\"in\"/>\n    <arg name=\"properties\" type=\"a{sv}\" direction=\"out\"/>\n  </method>\n  <signal name=\"PropertiesChanged\">\n    <arg name=\"interface\" type=\"s\"/>\n    <arg name=\"changed_properties\" type=\"a{sv}\"/>\n    <arg name=\"invalidated_properties\" type=\"as\"/>\n  </signal>\n</interface>\n";
    internal const string IntrospectableIntrospectXml = "<interface name=\"org.freedesktop.DBus.Introspectable\">\n  <method name=\"Introspect\">\n    <arg name=\"xml_data\" type=\"s\" direction=\"out\"/>\n  </method>\n</interface>\n";
    internal const string PeerIntrospectXml = "<interface name=\"org.freedesktop.DBus.Peer\">\n  <method name=\"Ping\" />\n  <method name=\"GetMachineId\">\n    <arg name=\"machine_uuid\" type=\"s\" direction=\"out\" />\n  </method>\n</interface>\n";

    private const string PeerInterfaceName = "org.freedesktop.DBus.Peer";
    private const string PropertiesInterfaceName = "org.freedesktop.DBus.Properties";
    private const string IntrospectableInterfaceName = "org.freedesktop.DBus.Introspectable";
    private const string ErrorUnknownMethod = "org.freedesktop.DBus.Error.UnknownMethod";
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";

    private static readonly string s_machineId = ResolveMachineId();

    public static bool EnablePeerInterface { get; set; } = true;

    public static bool EnablePropertiesInterface { get; set; } = true;

    public static bool EnableIntrospectableInterface { get; set; } = true;

    public static bool EnableGetNameOwner { get; set; } = true;

    internal static DBusMessage? TryHandlePeer(DBusMessage request)
    {
        if (!EnablePeerInterface)
        {
            return null;
        }

        if (request.Type != DBusMessageType.MethodCall)
        {
            return null;
        }

        if (!string.Equals(request.Interface, PeerInterfaceName, StringComparison.Ordinal))
        {
            return null;
        }

        return request.Member switch
        {
            "Ping" => request.CreateReply(),
            "GetMachineId" => request.CreateReply(s_machineId),
            _ => request.CreateError(ErrorUnknownMethod, "Unknown method")
        };
    }

    internal static DBusMessage? TryHandleProperties(DBusMessage request, IDBusObject obj)
    {
        if (!EnablePropertiesInterface)
        {
            return null;
        }

        if (request.Type != DBusMessageType.MethodCall)
        {
            return null;
        }

        if (!string.Equals(request.Interface, PropertiesInterfaceName, StringComparison.Ordinal))
        {
            return null;
        }

        switch (request.Member)
        {
            case "Get":
                {
                    if (request.Body.Count < 2 || request.Body[0] is not string iface || request.Body[1] is not string name)
                    {
                        return request.CreateError(ErrorUnknownMethod, "Invalid Get arguments.");
                    }

                    if (!obj.HasInterface(iface))
                    {
                        return request.CreateError(ErrorUnknownInterface, "Unknown interface");
                    }

                    if (!obj.TryGetProperty(iface, name, out var value))
                    {
                        return request.CreateError(ErrorUnknownProperty, "Unknown property");
                    }

                    return request.CreateReply(value);
                }
            case "GetAll":
                {
                    if (request.Body.Count < 1 || request.Body[0] is not string iface)
                    {
                        return request.CreateError(ErrorUnknownMethod, "Invalid GetAll arguments.");
                    }

                    if (!obj.HasInterface(iface))
                    {
                        return request.CreateError(ErrorUnknownInterface, "Unknown interface");
                    }

                    var props = obj.GetAllProperties(iface);
                    return request.CreateReply(props);
                }
            case "Set":
                {
                    if (request.Body.Count < 3 || request.Body[0] is not string iface || request.Body[1] is not string name || request.Body[2] is not DBusVariant value)
                        return request.CreateError(ErrorUnknownMethod, "Invalid Set arguments.");

                    if (!obj.HasInterface(iface))
                        return request.CreateError(ErrorUnknownInterface, "Unknown interface");

                    if (!obj.TrySetProperty(iface, name, value))
                        return request.CreateError(ErrorUnknownProperty, "Unknown property");

                    return request.CreateReply();
                }
            default:
                return request.CreateError(ErrorUnknownMethod, "Unknown method");
        }
    }

    internal static DBusMessage? TryHandleIntrospectable(DBusMessage request, IDBusObject obj)
    {
        if (!EnableIntrospectableInterface)
            return null;

        if (request.Type != DBusMessageType.MethodCall)
            return null;

        if (!string.Equals(request.Interface, IntrospectableInterfaceName, StringComparison.Ordinal))
            return null;

        return request.Member switch
        {
            "Introspect" => request.CreateReply(obj.IntrospectXml),
            _ => request.CreateError(ErrorUnknownMethod, "Unknown method")
        };
    }

    private static string ResolveMachineId()
    {
        string[] paths = ["/etc/machine-id", "/var/lib/dbus/machine-id"];
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var text = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            catch
            {
                // ignored
            }
        }

        return "unknown";
    }
}
