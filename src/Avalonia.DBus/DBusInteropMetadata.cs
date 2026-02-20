using System;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
sealed record DBusInteropMetadata
{
    public required Type ClrType { get; init; }

    public required string InterfaceName { get; init; }

    public CreateProxyFactory? CreateProxy { get; init; }
    public CreateHandlerFactory? CreateHandler { get; init; }
    public TrySetPropertyFactory? TrySetProperty { get; init; }

    public GetAllPropertiesFactory? GetAllPropertiesFactory { get; init; }

    public WriteIntrospectionXmlFactory? WriteIntrospectionXml { get; init; }
}
