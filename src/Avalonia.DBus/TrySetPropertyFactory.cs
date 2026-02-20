namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
delegate bool TrySetPropertyFactory(object target, string propertyName, object propertyValue);
