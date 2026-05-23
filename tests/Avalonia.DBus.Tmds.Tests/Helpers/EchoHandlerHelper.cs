using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Avalonia.DBus.Tmds.Tests.Helpers;

/// <summary>
/// Registers metadata for the echo test interface so that
/// <see cref="DBusConnection.RegisterObjects"/> can dispatch calls.
/// </summary>
internal static class EchoHandlerHelper
{
    private const string InterfaceName = "org.avalonia.dbus.tmds.Echo";
    private static readonly object Gate = new();
    private static bool s_registered;

    public static void EnsureRegistered()
    {
        if (s_registered) return;
        lock (Gate)
        {
            if (s_registered) return;
            DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
            {
                ClrType = typeof(EchoTarget),
                InterfaceName = InterfaceName,
                CreateHandler = static () => new EchoDispatcher(),
            });
            s_registered = true;
        }
    }
}

/// <summary>
/// Marker type for the echo test handler.
/// </summary>
internal sealed class EchoTarget;

/// <summary>
/// Handles incoming method calls for the echo test interface.
/// </summary>
internal sealed class EchoDispatcher : IDBusInterfaceCallDispatcher
{
    public Task<DBusMessage> Handle(IDBusConnection connection, object? target, DBusMessage message)
    {
        return message.Member switch
        {
            "Echo" when message.Body.Count > 0
                => Task.FromResult(CreateEchoReply(message)),
            "EchoAll"
                => Task.FromResult(CreateEchoAllReply(message)),
            "Add" when message.Body.Count >= 2
                => Task.FromResult(message.CreateReply((int)message.Body[0] + (int)message.Body[1])),
            "Concat" when message.Body.Count >= 2
                => Task.FromResult(message.CreateReply((string)message.Body[0] + (string)message.Body[1])),
            "Negate" when message.Body.Count > 0
                => Task.FromResult(message.CreateReply(-(long)message.Body[0])),
            _ => Task.FromResult(message.CreateError(
                "org.freedesktop.DBus.Error.UnknownMethod",
                $"Unknown method: {message.Member}"))
        };
    }

    private static DBusMessage CreateEchoReply(DBusMessage message)
    {
        var firstArg = message.Body[0];
        var firstTypeSig = ExtractFirstType(message.Signature.Value);
        var reply = message.CreateReply();
        reply.SetBodyWithSignature([firstArg], firstTypeSig);
        return reply;
    }

    private static DBusMessage CreateEchoAllReply(DBusMessage message)
    {
        var reply = message.CreateReply();
        reply.SetBodyWithSignature(message.Body.ToList(), message.Signature.Value);
        return reply;
    }

    /// <summary>
    /// Extracts the first complete type from a D-Bus signature string.
    /// </summary>
    internal static string ExtractFirstType(string sig)
    {
        if (string.IsNullOrEmpty(sig)) return sig;
        var idx = 0;
        SkipSingleType(sig, ref idx);
        return sig[..idx];
    }

    private static void SkipSingleType(string sig, ref int idx)
    {
        if (idx >= sig.Length) return;
        var c = sig[idx++];
        switch (c)
        {
            case 'y': case 'b': case 'n': case 'q': case 'i': case 'u':
            case 'x': case 't': case 'd': case 's': case 'o': case 'g':
            case 'v': case 'h':
                break;
            case 'a':
                SkipSingleType(sig, ref idx);
                break;
            case '(':
                while (idx < sig.Length && sig[idx] != ')')
                    SkipSingleType(sig, ref idx);
                if (idx < sig.Length) idx++; // skip ')'
                break;
            case '{':
                SkipSingleType(sig, ref idx); // key
                SkipSingleType(sig, ref idx); // value
                if (idx < sig.Length) idx++; // skip '}'
                break;
        }
    }
}
