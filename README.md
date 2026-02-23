# Avalonia.DBus

A managed D-Bus client and service library for .NET 8+, with an integrated Roslyn source generator for strongly-typed proxies and handlers.

Built for [Avalonia](https://avaloniaui.net/) but usable from any .NET application. Runs on Linux and macOS.

## Features

- Async/await API for method calls, signal subscriptions, and object registration
- Source generator that produces typed proxies and handler base classes from D-Bus XML interface definitions
- Full D-Bus type system support: variants, structs, dictionaries, arrays, object paths, signatures
- Bus-side methods integration (name ownership, introspection, name watching)

## Quick Start

```csharp
await using var connection = await DBusConnection.ConnectSessionAsync();

// Call a method
var reply = await connection.CallMethodAsync(
    "org.freedesktop.DBus",
    "/org/freedesktop/DBus",
    "org.freedesktop.DBus",
    "ListNames");

// Subscribe to signals
using var sub = await connection.SubscribeAsync(
    sender: null,
    path: "/org/freedesktop/Notifications",
    iface: "org.freedesktop.Notifications",
    member: "NotificationClosed",
    handler: async msg =>
    {
        var id = (uint)msg.Body[0];
        Console.WriteLine($"Notification {id} closed");
    });
```

## Source Generator

Add D-Bus XML interface files to your project and set the `DBusGeneratorMode` item metadata to `Proxy` or `Handler`. The source generator emits strongly-typed classes so you can call D-Bus methods and implement interfaces without manual message construction.

See the `samples/` directory for working examples, including an AT-SPI2 accessibility service implementation.

## Requirements

- .NET 8.0 or later
- `libdbus` installed on the host system

## License

MIT -- see [LICENSE.md](LICENSE.md).
