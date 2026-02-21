using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia.DBus;

/// <summary>
/// Built-in handler for <c>org.freedesktop.DBus.Introspectable</c>.
/// Auto-generates introspection XML from the registered handler keys
/// for the target path, including child sub-paths.
/// </summary>
internal sealed class BuiltInIntrospectionHandler(
    Func<string, IntrospectionData> resolveIntrospectionData)
    : IDBusInterfaceCallDispatcher
{
    public const string InterfaceName = "org.freedesktop.DBus.Introspectable";

    /// <summary>
    /// The path this handler is bound to.  Set after construction by the
    /// registration helper so we can look up our own path.
    /// </summary>
    internal string? BoundPath { get; set; }

    public Task<DBusMessage> Handle(IDBusConnection _, object? __, DBusMessage message)
    {
        if (!string.Equals(message.Member, "Introspect", StringComparison.Ordinal))
            return Task.FromResult(message.CreateError(
                "org.freedesktop.DBus.Error.UnknownMethod", "Unknown method"));

        var path = BoundPath ?? message.Path?.Value ?? "/";
        var data = resolveIntrospectionData(path);
        var xml = BuildXml(path, data);
        return Task.FromResult(message.CreateReply((object)xml));
    }

    private static string BuildXml(string path, IntrospectionData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE node PUBLIC \"-//freedesktop//DTD D-BUS Object Introspection 1.0//EN\"");
        sb.AppendLine(" \"http://www.freedesktop.org/standards/dbus/1.0/introspect.dtd\">");
        sb.AppendLine($"<node name=\"{EscapeXml(path)}\">");

        // Standard introspectable + properties interfaces are always present on real nodes
        if (data.Interfaces.Count > 0)
        {
            sb.AppendLine("  <interface name=\"org.freedesktop.DBus.Introspectable\">");
            sb.AppendLine("    <method name=\"Introspect\">");
            sb.AppendLine("      <arg name=\"xml_data\" type=\"s\" direction=\"out\"/>");
            sb.AppendLine("    </method>");
            sb.AppendLine("  </interface>");
            sb.AppendLine("  <interface name=\"org.freedesktop.DBus.Properties\">");
            sb.AppendLine("    <method name=\"Get\">");
            sb.AppendLine("      <arg name=\"interface_name\" type=\"s\" direction=\"in\"/>");
            sb.AppendLine("      <arg name=\"property_name\" type=\"s\" direction=\"in\"/>");
            sb.AppendLine("      <arg name=\"value\" type=\"v\" direction=\"out\"/>");
            sb.AppendLine("    </method>");
            sb.AppendLine("    <method name=\"GetAll\">");
            sb.AppendLine("      <arg name=\"interface_name\" type=\"s\" direction=\"in\"/>");
            sb.AppendLine("      <arg name=\"value\" type=\"a{sv}\" direction=\"out\"/>");
            sb.AppendLine("    </method>");
            sb.AppendLine("    <method name=\"Set\">");
            sb.AppendLine("      <arg name=\"interface_name\" type=\"s\" direction=\"in\"/>");
            sb.AppendLine("      <arg name=\"property_name\" type=\"s\" direction=\"in\"/>");
            sb.AppendLine("      <arg name=\"value\" type=\"v\" direction=\"in\"/>");
            sb.AppendLine("    </method>");
            sb.AppendLine("  </interface>");

            foreach (var (ifaceName, writeXml) in data.Interfaces)
            {
                if (writeXml is not null)
                {
                    writeXml(sb, "  ");
                }
                else
                {
                    sb.AppendLine($"  <interface name=\"{EscapeXml(ifaceName)}\">");
                    sb.AppendLine("  </interface>");
                }
            }
        }

        foreach (var child in data.ChildSegments)
        {
            sb.AppendLine($"  <node name=\"{EscapeXml(child)}\"/>");
        }

        sb.AppendLine("</node>");
        return sb.ToString();
    }

    private static string EscapeXml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

/// <summary>
/// Introspection data resolved for a given object path.
/// </summary>
internal readonly struct IntrospectionData
{
    /// <summary>
    /// D-Bus interfaces registered at this exact path.
    /// Each entry is (interfaceName, introspection-writer-or-null).
    /// </summary>
    public IReadOnlyList<(string Name, WriteIntrospectionXmlFactory? WriteXml)> Interfaces { get; init; }

    /// <summary>
    /// Immediate child path segments (e.g. for path "/org" this might
    /// contain "a11y" if "/org/a11y/..." is registered).
    /// </summary>
    public IReadOnlySet<string> ChildSegments { get; init; }
}
