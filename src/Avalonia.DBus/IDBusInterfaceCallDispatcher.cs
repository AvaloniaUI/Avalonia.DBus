using System.Threading.Tasks;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
interface IDBusInterfaceCallDispatcher
{
    Task<DBusMessage> Handle(IDBusConnection connection, object? target, DBusMessage message);
}
 
