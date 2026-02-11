# D-Bus .NET API Specification

## Overview

A .NET-friendly API for D-Bus built on top of libdbus. The design prioritizes minimal `IDisposable` usage - only connection objects are disposable, while `DBusMessage` and related types are pure managed classes/structs.

## Architecture

The API is split into two layers:

1. **Wire Layer** (`DBusWireConnection`, internal) - Low-level message transport
2. **High-Level Layer** (`DBusConnection`) - Service registration, signal subscriptions, method dispatch

---

## Type System

### Type Mapping

| D-Bus Type   | Signature | .NET Type                   |
|--------------|-----------|------------------------------|
| BYTE         | `y`       | `byte`                       |
| BOOLEAN      | `b`       | `bool`                       |
| INT16        | `n`       | `short`                      |
| UINT16       | `q`       | `ushort`                     |
| INT32        | `i`       | `int`                        |
| UINT32       | `u`       | `uint`                       |
| INT64        | `x`       | `long`                       |
| UINT64       | `t`       | `ulong`                      |
| DOUBLE       | `d`       | `double`                     |
| STRING       | `s`       | `string`                     |
| OBJECT_PATH  | `o`       | `DBusObjectPath`             |
| SIGNATURE    | `g`       | `DBusSignature`              |
| UNIX_FD      | `h`       | `DBusUnixFd`                 |
| ARRAY        | `a`       | `DBusArray<T>`               |
| STRUCT       | `(...)`   | `DBusStruct`                 |
| VARIANT      | `v`       | `DBusVariant`                |
| DICT_ENTRY   | `a{...}`  | `DBusDict<TKey, TValue>`     |

### Wrapper Types

```csharp
/// <summary>
/// Represents a D-Bus object path.
/// </summary>
public readonly struct DBusObjectPath : IEquatable<DBusObjectPath>
{
    public string Value { get; }
    
    public DBusObjectPath(string value);
    
    public static implicit operator string(DBusObjectPath path) => path.Value;
    public static explicit operator DBusObjectPath(string value) => new(value);
    
    // IEquatable, ToString, GetHashCode
}

/// <summary>
/// Represents a D-Bus type signature.
/// </summary>
public readonly struct DBusSignature : IEquatable<DBusSignature>
{
    public string Value { get; }
    
    public DBusSignature(string value);
    
    public static implicit operator string(DBusSignature signature) => signature.Value;
    public static explicit operator DBusSignature(string value) => new(value);
    
    // IEquatable, ToString, GetHashCode
}

/// <summary>
/// Represents a Unix file descriptor passed over D-Bus.
/// </summary>
public readonly struct DBusUnixFd : IEquatable<DBusUnixFd>
{
    public int Fd { get; }
    
    public DBusUnixFd(int fd);
    
    // IEquatable, ToString, GetHashCode
}
```

### Container Types

```csharp
/// <summary>
/// Represents a D-Bus array. Generic type parameter enables signature inference.
/// </summary>
public sealed class DBusArray<T> : IReadOnlyList<T>
{
    public DBusArray();
    public DBusArray(IEnumerable<T> items);
    public DBusArray(params T[] items);
    
    public int Count { get; }
    public T this[int index] { get; }
    
    public IEnumerator<T> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator();
}

/// <summary>
/// Represents a D-Bus dictionary (array of dict entries).
/// </summary>
public sealed class DBusDict<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public DBusDict();
    public DBusDict(IEnumerable<KeyValuePair<TKey, TValue>> items);
    
    public int Count { get; }
    public TValue this[TKey key] { get; }
    public IEnumerable<TKey> Keys { get; }
    public IEnumerable<TValue> Values { get; }
    
    public bool ContainsKey(TKey key);
    public bool TryGetValue(TKey key, out TValue value);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator();
}

/// <summary>
/// Represents a D-Bus struct (sequence of typed fields).
/// </summary>
public sealed class DBusStruct : IReadOnlyList<object>
{
    public DBusStruct(params object[] fields);
    public DBusStruct(IEnumerable<object> fields);
    
    public int Count { get; }
    public object this[int index] { get; }
    
    public IEnumerator<object> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator();
}

/// <summary>
/// Represents a D-Bus variant (dynamically typed value).
/// </summary>
public sealed class DBusVariant
{
    /// <summary>
    /// The D-Bus type signature of the contained value.
    /// </summary>
    public DBusSignature Signature { get; }
    
    /// <summary>
    /// The contained value.
    /// </summary>
    public object Value { get; }
    
    /// <summary>
    /// Creates a variant with an inferred signature based on the value's .NET type.
    /// </summary>
    public DBusVariant(object value);
    
    /// <summary>
    /// Creates a variant with an explicit signature.
    /// </summary>
    public DBusVariant(DBusSignature signature, object value);
}
```

---

## Message Types

```csharp
public enum DBusMessageType
{
    Invalid = 0,
    MethodCall = 1,
    MethodReturn = 2,
    Error = 3,
    Signal = 4
}

[Flags]
public enum DBusMessageFlags
{
    None = 0,
    NoReplyExpected = 0x1,
    NoAutoStart = 0x2,
    AllowInteractiveAuthorization = 0x4
}
```

---

## DBusMessage

A pure managed class representing a D-Bus message. Not `IDisposable` - data is copied from the native libdbus message into managed memory.

```csharp
public sealed class DBusMessage
{
    // ============ Header Fields ============
    
    /// <summary>
    /// The message type.
    /// </summary>
    public DBusMessageType Type { get; init; }
    
    /// <summary>
    /// Message flags.
    /// </summary>
    public DBusMessageFlags Flags { get; init; }
    
    /// <summary>
    /// Message serial number. Assigned by the connection when sent.
    /// </summary>
    public uint Serial { get; }
    
    /// <summary>
    /// For METHOD_RETURN and ERROR: the serial of the METHOD_CALL being replied to.
    /// </summary>
    public uint ReplySerial { get; init; }
    
    /// <summary>
    /// Object path (for METHOD_CALL and SIGNAL).
    /// </summary>
    public DBusObjectPath? Path { get; init; }
    
    /// <summary>
    /// Interface name.
    /// </summary>
    public string? Interface { get; init; }
    
    /// <summary>
    /// Method or signal name.
    /// </summary>
    public string? Member { get; init; }
    
    /// <summary>
    /// Error name (for ERROR messages).
    /// </summary>
    public string? ErrorName { get; init; }
    
    /// <summary>
    /// Destination bus name.
    /// </summary>
    public string? Destination { get; init; }
    
    /// <summary>
    /// Sender's unique bus name. Set by the message bus.
    /// </summary>
    public string? Sender { get; }
    
    /// <summary>
    /// Type signature of the message body. Computed from Body contents.
    /// </summary>
    public DBusSignature Signature { get; }
    
    // ============ Body ============
    
    /// <summary>
    /// Message body as a list of typed values.
    /// </summary>
    public IReadOnlyList<object> Body { get; init; }
    
    // ============ Factory Methods ============
    
    /// <summary>
    /// Creates a METHOD_CALL message.
    /// </summary>
    public static DBusMessage CreateMethodCall(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        params object[] body);
    
    /// <summary>
    /// Creates a SIGNAL message.
    /// </summary>
    public static DBusMessage CreateSignal(
        DBusObjectPath path,
        string iface,
        string member,
        params object[] body);
    
    /// <summary>
    /// Creates a METHOD_RETURN message in reply to this message.
    /// </summary>
    public DBusMessage CreateReply(params object[] body);
    
    /// <summary>
    /// Creates an ERROR message in reply to this message.
    /// </summary>
    public DBusMessage CreateError(string errorName, string? errorMessage = null);
    
    // ============ Convenience Methods ============
    
    /// <summary>
    /// Returns true if this is a METHOD_CALL for the specified interface and member.
    /// </summary>
    public bool IsMethodCall(string iface, string member);
    
    /// <summary>
    /// Returns true if this is a SIGNAL for the specified interface and member.
    /// </summary>
    public bool IsSignal(string iface, string member);
    
    /// <summary>
    /// Returns true if this is an ERROR with the specified error name.
    /// </summary>
    public bool IsError(string errorName);
}
```

---

## Wire Layer

Low-level connection handling raw message transport. This is the only `IDisposable` type in the API.

```csharp
internal sealed class DBusWireConnection : IAsyncDisposable
{
    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    /// <param name="address">
    /// D-Bus address string (e.g., "unix:path=/run/dbus/system_bus_socket")
    /// or well-known bus type ("session", "system").
    /// </param>
    public static Task<DBusWireConnection> ConnectAsync(
        string address,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static Task<DBusWireConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static Task<DBusWireConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// The unique name assigned by the message bus (e.g., ":1.42").
    /// Null if not connected to a message bus.
    /// </summary>
    public string? UniqueName { get; }
    
    /// <summary>
    /// Sends a message without waiting for a reply.
    /// </summary>
    public ValueTask SendAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a message and waits for a reply.
    /// </summary>
    public Task<DBusMessage> SendWithReplyAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Receives incoming messages (METHOD_CALL, SIGNAL, etc.).
    /// Used for implementing services.
    /// </summary>
    public IAsyncEnumerable<DBusMessage> ReceiveAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public ValueTask DisposeAsync();
}
```

---

## High-Level Layer

Wraps `DBusWireConnection` with service registration, signal subscriptions, and method dispatch.
Service-side dispatch is descriptor-driven and uses explicit exported-target registration.

```csharp
public sealed class DBusConnection : IAsyncDisposable
{
    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    public static Task<DBusConnection> ConnectAsync(
        string address,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static Task<DBusConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static Task<DBusConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default);
    
    // The underlying wire connection is internal; consumers should use DBusConnection's public API.
    internal DBusWireConnection Wire { get; }

    /// <summary>
    /// Sends a pre-constructed message without waiting for a reply.
    /// </summary>
    public Task SendMessageAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// The unique name assigned by the message bus (e.g., ":1.42").
    /// </summary>
    public Task<string?> GetUniqueNameAsync();
    
    // ============ Client: Method Calls ============
    
    /// <summary>
    /// Calls a method on a remote object and returns the reply.
    /// </summary>
    public Task<DBusMessage> CallMethodAsync(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        params object[] args);
    
    /// <summary>
    /// Calls a method on a remote object and returns the reply.
    /// </summary>
    public Task<DBusMessage> CallMethodAsync(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        CancellationToken cancellationToken,
        params object[] args);
    
    // ============ Client: Signals ============
    
    /// <summary>
    /// Subscribes to signals matching the specified criteria.
    /// </summary>
    /// <param name="sender">Filter by sender (null for any).</param>
    /// <param name="path">Filter by object path (null for any).</param>
    /// <param name="iface">Interface name.</param>
    /// <param name="member">Signal name.</param>
    /// <param name="handler">Async callback invoked for each matching signal.</param>
    /// <param name="synchronizationContext">
    /// Optional synchronization context to invoke the handler on (e.g., UI thread).
    /// If null, handler is invoked on the connection's internal thread.
    /// </param>
    /// <returns>Disposable that unsubscribes when disposed.</returns>
    public Task<IDisposable> SubscribeAsync(
        string? sender,
        DBusObjectPath? path,
        string iface,
        string member,
        Func<DBusMessage, Task> handler,
        SynchronizationContext? synchronizationContext = null);
    
    // ============ Server: Name Ownership ============
    
    /// <summary>
    /// Requests ownership of a bus name.
    /// </summary>
    public Task<DBusRequestNameReply> RequestNameAsync(
        string name,
        DBusRequestNameFlags flags = DBusRequestNameFlags.None,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Releases ownership of a bus name.
    /// </summary>
    public Task ReleaseNameAsync(
        string name,
        CancellationToken cancellationToken = default);
    
    // ============ Server: Object Registration ============
    
    /// <summary>
    /// Registers a low-level delegate handler for a path/interface pair.
    /// </summary>
    public IDisposable RegisterObject(
        DBusObjectPath path,
        string iface,
        Func<DBusConnection, DBusMessage, Task<DBusMessage>> handler,
        SynchronizationContext? synchronizationContext = null);

    /// <summary>
    /// Registers an exported target at a full object path.
    /// Target must be a DBusExportedTarget.
    /// </summary>
    public IDisposable Register(
        string fullPath,
        DBusExportedTarget target,
        SynchronizationContext? synchronizationContext = null);

    /// <summary>
    /// Applies add/remove/replace registration operations atomically.
    /// </summary>
    public void ApplyRegistrationBatch(
        IEnumerable<DBusRegistrationOperation> operations,
        SynchronizationContext? synchronizationContext = null);

    /// <summary>
    /// Returns direct child object paths for the provided parent path.
    /// </summary>
    public IReadOnlyList<string> QueryChildren(string path);

    /// <summary>
    /// Returns true when an exported target is registered at the exact path.
    /// </summary>
    public bool IsPathRegistered(string path);
    
    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public ValueTask DisposeAsync();
}

[Flags]
public enum DBusRequestNameFlags
{
    None = 0,
    
    /// <summary>
    /// Allow other connections to take over this name.
    /// </summary>
    AllowReplacement = 0x1,
    
    /// <summary>
    /// Try to take over the name from the current owner.
    /// </summary>
    ReplaceExisting = 0x2,
    
    /// <summary>
    /// Don't queue for the name if it's already owned.
    /// </summary>
    DoNotQueue = 0x4
}

public enum DBusRequestNameReply
{
    /// <summary>
    /// The caller is now the primary owner of the name.
    /// </summary>
    PrimaryOwner = 1,
    
    /// <summary>
    /// The caller is in the queue to own the name.
    /// </summary>
    InQueue = 2,
    
    /// <summary>
    /// The name already has an owner and DoNotQueue was specified.
    /// </summary>
    Exists = 3,
    
    /// <summary>
    /// The caller already owns the name.
    /// </summary>
    AlreadyOwner = 4
}

public sealed class DBusExportedTargetBindingBuilder
{
    public void Bind<TInterface>(
        TInterface target,
        SynchronizationContext? synchronizationContext = null)
        where TInterface : class;
}

public sealed class DBusExportedTarget
{
    public static DBusExportedTarget Create(
        object target,
        Action<DBusExportedTargetBindingBuilder> configure);

    public static DBusExportedTarget Create<TInterface>(
        TInterface target,
        SynchronizationContext? synchronizationContext = null)
        where TInterface : class;
}

public readonly struct DBusRegistrationOperation
{
    public string Path { get; }
    public DBusExportedTarget? Target { get; }

    public static DBusRegistrationOperation Add(string fullPath, DBusExportedTarget target);
    public static DBusRegistrationOperation Remove(string fullPath);
    public static DBusRegistrationOperation Replace(string fullPath, DBusExportedTarget target);
}

public interface IDBusInterfaceCallDispatcher
{
    Task<DBusMessage> Handle(DBusMessage message, DBusConnection connection, object target);
}

public sealed class DBusInterfaceDescriptor
{
    public string InterfaceName { get; init; }
    public Type ClrInterfaceType { get; init; }
    public string IntrospectionXml { get; init; }
    public IDBusInterfaceCallDispatcher Dispatcher { get; init; }
    public IReadOnlyDictionary<string, DBusPropertyDescriptor> Properties { get; init; }
    public IReadOnlyDictionary<string, DBusMethodDescriptor> Methods { get; init; }
}

public static class DBusGeneratedMetadata
{
    public static void Register(DBusInterfaceDescriptor descriptor);
}

public interface IDBusSubtreeLifecycle
{
    void OnConnectedToTree(DBusConnection connection, string fullPath);
    void OnDisconnectedFromTree(DBusConnection connection, string fullPath);
}

public readonly struct DBusSubtreeRegistration
{
    public string FullPath { get; }
    public DBusExportedTarget Target { get; }
    public IDBusSubtreeLifecycle? Lifecycle { get; }
}

public sealed class DBusSubtreeRegistrationHelper
{
    public DBusSubtreeRegistrationHelper(DBusConnection connection);
    public void ApplySnapshot(
        IEnumerable<DBusSubtreeRegistration> registrations,
        SynchronizationContext? synchronizationContext = null);
    public void Clear(SynchronizationContext? synchronizationContext = null);
}
```

Legacy service contracts `IDBusObject`, `IDBusInterfaceHandler`, `DBusObject`, and `DBusConnection.Root` were removed.
For migration guidance, see `docs/dispatch-refactor-migration.md`.

---

## Error Handling

```csharp
/// <summary>
/// Exception thrown when a D-Bus method call returns an ERROR message.
/// </summary>
public class DBusException : Exception
{
    /// <summary>
    /// The D-Bus error name (e.g., "org.freedesktop.DBus.Error.ServiceUnknown").
    /// </summary>
    public string ErrorName { get; }
    
    /// <summary>
    /// The original ERROR message.
    /// </summary>
    public DBusMessage Message { get; }
    
    public DBusException(string errorName, string? message, DBusMessage dbusMessage);
}
```

---

## Usage Examples

### Client: Calling a Method

```csharp
await using var connection = await DBusConnection.ConnectSessionAsync();

var reply = await connection.CallMethodAsync(
    "org.freedesktop.Notifications",
    (DBusObjectPath)"/org/freedesktop/Notifications",
    "org.freedesktop.Notifications",
    "Notify",
    "MyApp",                           // app_name
    0u,                                // replaces_id
    "dialog-information",              // app_icon
    "Hello",                           // summary
    "Hello from .NET!",                // body
    new DBusArray<string>(),           // actions
    new DBusDict<string, DBusVariant>(), // hints
    5000                               // expire_timeout
);

var notificationId = (uint)reply.Body[0];
```

### Client: Subscribing to Signals

```csharp
await using var connection = await DBusConnection.ConnectSessionAsync();

using var subscription = await connection.SubscribeAsync(
    sender: null,
    path: (DBusObjectPath)"/org/freedesktop/Notifications",
    iface: "org.freedesktop.Notifications",
    member: "NotificationClosed",
    handler: async message =>
    {
        var id = (uint)message.Body[0];
        var reason = (uint)message.Body[1];
        Console.WriteLine($"Notification {id} closed with reason {reason}");
    });

// Keep running...
await Task.Delay(Timeout.Infinite);
```

### Server: Implementing a Service

```csharp
await using var connection = await DBusConnection.ConnectSessionAsync();

// Request a well-known name
var result = await connection.RequestNameAsync(
    "com.example.MyService",
    DBusRequestNameFlags.DoNotQueue);

if (result != DBusRequestNameReply.PrimaryOwner)
    throw new Exception("Failed to acquire bus name");

// Register exported target using generated Service-mode bindings.
var service = new ComExampleMyServiceImpl();
using var registration = connection.Register(
    "/com/example/MyService",
    ComExampleMyServiceExport.CreateTarget(service));

// Keep running...
await Task.Delay(Timeout.Infinite);
```

### Wire Layer: Low-Level Access (Internal)

```csharp
await using var connection = await DBusConnection.ConnectSessionAsync();

// Manual message construction
var message = DBusMessage.CreateMethodCall(
    "org.freedesktop.DBus",
    (DBusObjectPath)"/org/freedesktop/DBus",
    "org.freedesktop.DBus",
    "ListNames");

var reply = await connection.CallMethodAsync(
    "org.freedesktop.DBus",
    (DBusObjectPath)"/org/freedesktop/DBus",
    "org.freedesktop.DBus",
    "ListNames");
var names = (DBusArray<string>)reply.Body[0];

foreach (var name in names)
    Console.WriteLine(name);
```

---

## Implementation Notes

### Native Interop (libdbus)

- `DBusWireConnection` owns the native `DBusConnection*` handle
- Messages are marshaled to/from native format using `dbus_message_iter_*` APIs
- Native `DBusMessage*` is created temporarily during send/receive, data is copied to managed `DBusMessage`
- Connection reading loop runs on a background thread, dispatches to `ReceiveAsync` subscribers

### Signature Inference

The `DBusVariant` constructor that takes only a value must infer the D-Bus signature from the .NET type:

| .NET Type | Inferred Signature |
|-----------|-------------------|
| `byte` | `y` |
| `bool` | `b` |
| `short` | `n` |
| `ushort` | `q` |
| `int` | `i` |
| `uint` | `u` |
| `long` | `x` |
| `ulong` | `t` |
| `double` | `d` |
| `string` | `s` |
| `DBusObjectPath` | `o` |
| `DBusSignature` | `g` |
| `DBusUnixFd` | `h` |
| `DBusArray<T>` | `a` + infer(T) |
| `DBusDict<K,V>` | `a{` + infer(K) + infer(V) + `}` |
| `DBusStruct` | `(` + infer each element + `)` |
| `DBusVariant` | `v` |

### Thread Safety

- `DBusWireConnection` is thread-safe for concurrent `SendAsync` calls
- `DBusConnection` is thread-safe for all operations
- Signal/method handlers may be invoked concurrently
