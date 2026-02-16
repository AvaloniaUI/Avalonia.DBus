using System.Collections.Generic;

namespace Avalonia.DBus;

public delegate IReadOnlyDictionary<string, DBusVariant> GetAllPropertiesFactory(object target);