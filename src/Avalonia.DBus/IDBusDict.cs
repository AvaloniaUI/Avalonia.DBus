using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

internal interface IDBusDict
{
    Type KeyType { get; }

    Type ValueType { get; }

    IEnumerable<KeyValuePair<object?, object?>> Entries { get; }
}