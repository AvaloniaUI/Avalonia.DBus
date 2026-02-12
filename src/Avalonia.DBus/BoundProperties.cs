using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

internal record BoundProperties(
    Func<string, DBusVariant?>? TryGet,
    Func<string, DBusVariant, bool>? TrySet,
    Func<IReadOnlyDictionary<string, DBusVariant>>? GetAll); 