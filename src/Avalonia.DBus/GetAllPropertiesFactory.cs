using System.Collections.Generic;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
delegate IReadOnlyDictionary<string, DBusVariant> GetAllPropertiesFactory(object target);
