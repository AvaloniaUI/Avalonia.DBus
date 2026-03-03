using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
static class DBusStructSignatureRegistry
{
    private static readonly object Gate = new();
    private static Dictionary<Type, string> s_signatures = new();

    public static void Register(Type type, string signature)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (string.IsNullOrWhiteSpace(signature))
            throw new ArgumentException("Signature is required.", nameof(signature));

        lock (Gate)
        {
            var updated = new Dictionary<Type, string>(s_signatures)
            {
                [type] = signature
            };
            s_signatures = updated;
        }
    }

    internal static bool TryGetSignature(Type type, out string? signature)
    {
        var snapshot = s_signatures;
        return snapshot.TryGetValue(type, out signature);
    }
}
