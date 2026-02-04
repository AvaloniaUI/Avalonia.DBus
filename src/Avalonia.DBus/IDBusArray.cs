using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

internal interface IDBusArray
{
    Type ElementType { get; }

    string? ElementSignature { get; }

    IEnumerable<object?> Items { get; }
}