using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

public sealed class DBusInterfaceDescriptor
{
    public required string InterfaceName { get; init; }

    public required Type ClrInterfaceType { get; init; }

    public required string IntrospectionXml { get; init; }

    public required IDBusInterfaceCallDispatcher Dispatcher { get; init; }

    public required IReadOnlyDictionary<string, DBusPropertyDescriptor> Properties { get; init; }

    public required IReadOnlyDictionary<string, DBusMethodDescriptor> Methods { get; init; }
}